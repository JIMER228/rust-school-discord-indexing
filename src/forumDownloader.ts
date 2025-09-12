import * as fs from 'fs-extra';
import * as path from 'path';
import fetch, { type Response } from 'node-fetch';
import * as yauzl from 'yauzl';
import * as tar from 'tar';
import { 
    ForumChannel, 
    ThreadChannel, 
    Message, 
    Attachment,
    Collection,
    FetchedThreads 
} from 'discord.js';
import { config } from './config.js';
import type { DownloadStats, TopicInfo, TopicMessage, ProgressState } from './types.js';
import { Readable } from 'stream';

/**
 * Класс для загрузки всех файлов из Discord форум-каналов
 */
export class ForumDownloader {
    private readonly downloadStats: DownloadStats = {
        totalFiles: 0,
        downloadedFiles: 0,
        skippedFiles: 0,
        errors: 0,
        processedTopics: 0,
        skippedTopics: 0
    };
    
    // private readonly activeDownloads = new Set<Promise<void>>();  // Убрали параллельное скачивание для надежности
    private readonly progressFile = path.join(config.downloadDir, '.progress.json');
    private progressState: ProgressState | null = null;

    /**
     * Скачивает все файлы из форум-канала с возможностью продолжения
     */
    public async downloadAllFromForum(forum: ForumChannel): Promise<void> {
        console.log(`🎯 Начинаем загрузку из форума: "${forum.name}"`);
        
        await fs.ensureDir(config.downloadDir);
        
        // Загружаем или создаем прогресс
        await this.loadProgress();

        const allThreads = await this.getAllThreads(forum);
        console.log(`📄 Найдено тредов: ${allThreads.length}`);

        // Инициализируем прогресс если нужно
        if (!this.progressState) {
            this.progressState = {
                lastProcessedIndex: -1,
                totalTopics: allThreads.length,
                processedTopics: [],
                startTime: new Date().toISOString(),
                lastUpdateTime: new Date().toISOString(),
                stats: { ...this.downloadStats }
            };
        }
        
        const startIndex = this.progressState.lastProcessedIndex + 1;
        if (startIndex > 0) {
            console.log(`🔄 Продолжаем с топика ${startIndex + 1}/${allThreads.length}`);
        }

        for (let i = startIndex; i < allThreads.length; i++) {
            const thread = allThreads[i];
            if (thread === undefined) {
                continue;
            }
            
            console.log(`\n📂 [${i + 1}/${allThreads.length}] Обрабатываем тред: "${thread.name}"`);
            
            try {
                await this.processThread(thread);
                
                // Обновляем прогресс
                this.progressState.lastProcessedIndex = i;
                this.progressState.processedTopics.push(thread.id);
                this.progressState.lastUpdateTime = new Date().toISOString();
                this.progressState.stats = { ...this.downloadStats };
                
                await this.saveProgress();
                await this.sleep(config.requestDelay);
                
            } catch (error) {
                const errorMessage = this.getErrorMessage(error);
                console.error(`❌ Ошибка при обработке треда "${thread.name}":`, errorMessage);
                this.downloadStats.errors++;
                
                // Сохраняем прогресс даже при ошибке
                this.progressState.lastProcessedIndex = i;
                this.progressState.lastUpdateTime = new Date().toISOString();
                this.progressState.stats = { ...this.downloadStats };
                await this.saveProgress();
                
                // Продолжаем обработку следующего треда
                await this.sleep(config.requestDelay);
            }
        }

        // Удаляем файл прогресса после завершения
        await this.clearProgress();
        this.printFinalStats();
    }

    /**
     * Загружает прогресс из файла
     */
    private async loadProgress(): Promise<void> {
        try {
            if (await fs.pathExists(this.progressFile)) {
                const progressData = await fs.readFile(this.progressFile, 'utf8');
                this.progressState = JSON.parse(progressData) as ProgressState;
                
                // Восстанавливаем статистику
                this.downloadStats.processedTopics = this.progressState.stats.processedTopics;
                this.downloadStats.skippedTopics = this.progressState.stats.skippedTopics;
                this.downloadStats.downloadedFiles = this.progressState.stats.downloadedFiles;
                this.downloadStats.skippedFiles = this.progressState.stats.skippedFiles;
                this.downloadStats.errors = this.progressState.stats.errors;
                
                const elapsed = new Date().getTime() - new Date(this.progressState.startTime).getTime();
                const elapsedMinutes = Math.round(elapsed / 60000);
                
                console.log(`📂 Найден сохраненный прогресс:`);
                console.log(`   - Обработано: ${this.progressState.processedTopics.length} топиков`);
                console.log(`   - Последний индекс: ${this.progressState.lastProcessedIndex}`);
                console.log(`   - Время работы: ${elapsedMinutes} мин`);
            }
        } catch (error) {
            console.warn('⚠️ Ошибка при загрузке прогресса:', this.getErrorMessage(error));
            this.progressState = null;
        }
    }

    /**
     * Сохраняет прогресс в файл
     */
    private async saveProgress(): Promise<void> {
        try {
            if (this.progressState) {
                await fs.writeFile(this.progressFile, JSON.stringify(this.progressState, null, 2), 'utf8');
            }
        } catch (error) {
            console.warn('⚠️ Ошибка при сохранении прогресса:', this.getErrorMessage(error));
        }
    }

    /**
     * Очищает файл прогресса после завершения
     */
    private async clearProgress(): Promise<void> {
        try {
            if (await fs.pathExists(this.progressFile)) {
                await fs.unlink(this.progressFile);
                console.log('🗑️ Файл прогресса удален');
            }
        } catch (error) {
            console.warn('⚠️ Ошибка при удалении прогресса:', this.getErrorMessage(error));
        }
    }

    /**
     * Получает все треды форума (активные и архивные)
     */
    private async getAllThreads(forum: ForumChannel): Promise<readonly ThreadChannel[]> {
        const allThreads: ThreadChannel[] = [];

        try {
            console.log('🔍 Получаем активные треды...');
            const activeThreads = await forum.threads.fetchActive(false);
            allThreads.push(...Array.from(activeThreads.threads.values()));
            console.log(`   ✅ Активных тредов: ${activeThreads.threads.size}`);

            console.log('🔍 Получаем архивные публичные треды...');
            
            // Получаем ВСЕ архивные треды порциями
            let hasMoreArchived = true;
            let beforeId: string | undefined;
            let totalPublicArchived = 0;
            
            while (hasMoreArchived) {
                const options: { limit: number; type: 'public'; before?: string } = {
                    limit: 100,
                    type: 'public'
                };
                if (beforeId !== undefined) {
                    options.before = beforeId;
                }
                
                const publicArchivedThreads: FetchedThreads = await forum.threads.fetchArchived(options);
                
                if (publicArchivedThreads.threads.size === 0) {
                    hasMoreArchived = false;
                    break;
                }
                
                allThreads.push(...Array.from(publicArchivedThreads.threads.values()));
                totalPublicArchived += publicArchivedThreads.threads.size;
                
                // Получаем ID последнего треда для следующей порции
                const threads = Array.from(publicArchivedThreads.threads.values());
                beforeId = threads[threads.length - 1]?.id;
                
                console.log(`   → Получено ${publicArchivedThreads.threads.size} архивных тредов (всего: ${totalPublicArchived})`);
                
                // Пауза между запросами
                await this.sleep(1000);
            }
            
            console.log(`   ✅ Всего архивных публичных тредов: ${totalPublicArchived}`);

            try {
                console.log('🔍 Получаем архивные приватные треды...');
                
                hasMoreArchived = true;
                beforeId = undefined;
                let totalPrivateArchived = 0;
                
                while (hasMoreArchived) {
                    const options: { limit: number; type: 'private'; before?: string } = {
                        limit: 100,
                        type: 'private'
                    };
                    if (beforeId !== undefined) {
                        options.before = beforeId;
                    }
                    
                    const privateArchivedThreads: FetchedThreads = await forum.threads.fetchArchived(options);
                    
                    if (privateArchivedThreads.threads.size === 0) {
                        hasMoreArchived = false;
                        break;
                    }
                    
                    allThreads.push(...Array.from(privateArchivedThreads.threads.values()));
                    totalPrivateArchived += privateArchivedThreads.threads.size;
                    
                    const threads = Array.from(privateArchivedThreads.threads.values());
                    beforeId = threads[threads.length - 1]?.id;
                    
                    console.log(`   → Получено ${privateArchivedThreads.threads.size} приватных архивных тредов (всего: ${totalPrivateArchived})`);
                    
                    await this.sleep(1000);
                }
                
                console.log(`   ✅ Всего архивных приватных тредов: ${totalPrivateArchived}`);
            } catch (error) {
                const errorMessage = this.getErrorMessage(error);
                console.warn('⚠️ Нет доступа к приватным архивным тредам:', errorMessage);
            }

        } catch (error) {
            console.error('❌ Ошибка при получении тредов:', error);
            throw error;
        }

        return allThreads as readonly ThreadChannel[];
    }

    /**
     * Обрабатывает один тред - собирает сообщения и скачивает релевантные файлы
     */
    private async processThread(thread: ThreadChannel): Promise<void> {
        console.log(`   🔍 Анализируем тред на наличие .cs файлов и архивов...`);
        
        // Собираем всю информацию о треде
        const topicInfo = await this.collectTopicInfo(thread);
        
        if (!topicInfo.hasRelevantFiles) {
            console.log(`   ⏭️ Пропускаем тред (нет .cs файлов или архивов)`);
            this.downloadStats.skippedTopics++;
            return;
        }

        const sanitizedThreadName = this.sanitizeFileName(thread.name);
        const topicDir = path.join(config.downloadDir, 'topics', sanitizedThreadName);
        await fs.ensureDir(topicDir);

        console.log(`   📁 Создаем папку: topics/${sanitizedThreadName}`);

        // Генерируем README.md из сообщений
        await this.generateReadme(topicInfo, topicDir);
        
        // Скачиваем файлы напрямую из сообщений с вложениями
        await this.downloadFilesFromTopicMessages(topicInfo, thread, topicDir);
        
        this.downloadStats.processedTopics++;
        console.log(`   ✅ Тред обработан: ${topicInfo.messages.length} сообщений`);
    }

    /**
     * Собирает информацию о треде и проверяет наличие релевантных файлов
     */
    private async collectTopicInfo(thread: ThreadChannel): Promise<TopicInfo> {
        const topicInfo: TopicInfo = {
            name: thread.name,
            messages: [],
            hasRelevantFiles: false
        };

        let lastMessageId: string | undefined;
        
        while (true) {
            try {
                const options: { limit: number; before?: string } = { limit: 100 };
                if (lastMessageId !== undefined) {
                    options.before = lastMessageId;
                }

                const messages: Collection<string, Message> = await thread.messages.fetch(options);
                
                if (messages.size === 0) {
                    break;
                }

                for (const discordMessage of Array.from(messages.values()).reverse()) {
                    const topicMessage: TopicMessage = {
                        id: discordMessage.id,
                        author: discordMessage.author.displayName || discordMessage.author.username,
                        content: discordMessage.content || '',
                        timestamp: discordMessage.createdAt.toISOString(),
                        attachments: []
                    };

                    // Проверяем вложения
                    for (const attachment of discordMessage.attachments.values()) {
                        topicMessage.attachments.push(attachment.name);
                        
                        if (this.shouldDownloadFile(attachment.name)) {
                            topicInfo.hasRelevantFiles = true;
                        }
                    }

                    topicInfo.messages.push(topicMessage);
                }

                const lastMessage = messages.last();
                if (lastMessage === undefined) {
                    break;
                }
                
                lastMessageId = lastMessage.id;
                await this.sleep(config.requestDelay / 2);

            } catch (error) {
                const errorMessage = this.getErrorMessage(error);
                console.error(`❌ Ошибка при получении сообщений треда "${thread.name}":`, errorMessage);
                break;
            }
        }

        // Сортируем сообщения по времени (старые первые)
        topicInfo.messages.sort((a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime());
        
        return topicInfo;
    }

    /**
     * Генерирует README.md из сообщений топика
     */
    private async generateReadme(topicInfo: TopicInfo, topicDir: string): Promise<void> {
        const readmePath = path.join(topicDir, 'README.md');
        
        let readmeContent = `# ${topicInfo.name}\n\n`;
        readmeContent += `**Всего сообщений:** ${topicInfo.messages.length}\n\n`;
        readmeContent += `---\n\n`;

        for (const message of topicInfo.messages) {
            readmeContent += `## 💬 ${message.author} \n`;
            readmeContent += `**Время:** ${new Date(message.timestamp).toLocaleString('ru-RU')}\n\n`;
            
            if (message.content.trim()) {
                readmeContent += `${message.content}\n\n`;
            }
            
            if (message.attachments.length > 0) {
                readmeContent += `**Вложения:**\n`;
                for (const attachment of message.attachments) {
                    readmeContent += `- 📎 ${attachment}\n`;
                }
                readmeContent += `\n`;
            }
            
            readmeContent += `---\n\n`;
        }

        await fs.writeFile(readmePath, readmeContent, 'utf8');
        console.log(`   📝 Создан README.md`);
    }

    /**
     * Скачивает файлы из сообщений с вложениями
     */
    private async downloadFilesFromTopicMessages(topicInfo: TopicInfo, thread: ThreadChannel, topicDir: string): Promise<void> {
        // Находим сообщения с вложениями
        const messagesWithAttachments = topicInfo.messages.filter(msg => msg.attachments.length > 0);
        
        if (messagesWithAttachments.length === 0) {
            return;
        }
        
        console.log(`   📦 Обрабатываем ${messagesWithAttachments.length} сообщений с вложениями`);
        
        for (const topicMessage of messagesWithAttachments) {
            try {
                // Получаем реальное сообщение Discord для доступа к Attachment объектам
                const discordMessage = await thread.messages.fetch(topicMessage.id);
                
                console.log(`     📎 Обрабатываем сообщение от ${topicMessage.author} с ${discordMessage.attachments.size} вложениями`);
                
                await this.downloadAttachmentsFromMessage(discordMessage, topicDir);
                await this.sleep(500); // Пауза между сообщениями
                
            } catch (error) {
                const errorMessage = this.getErrorMessage(error);
                console.error(`     ❌ Ошибка при обработке сообщения ${topicMessage.id}:`, errorMessage);
                this.downloadStats.errors++;
            }
        }
    }

    /**
     * Скачивает все вложения из одного сообщения (включая несколько архивов и .cs файлов)
     */
    private async downloadAttachmentsFromMessage(message: Message, topicDir: string): Promise<void> {
        const attachmentsList = Array.from(message.attachments.values());
        const relevantFiles = attachmentsList.filter(att => this.shouldDownloadFile(att.name));
        
        if (relevantFiles.length === 0) {
            return;
        }
        
        console.log(`     📁 Обрабатываем ${relevantFiles.length} релевантных вложений из ${attachmentsList.length}`);
        
        // Обрабатываем каждое вложение поочередно для надежности
        for (const attachment of relevantFiles) {
            try {
                await this.downloadSingleAttachment(attachment, message, topicDir);
                // Небольшая пауза между скачиванием файлов
                await this.sleep(500);
            } catch (error) {
                const errorMessage = this.getErrorMessage(error);
                console.error(`     ❌ Ошибка при скачивании ${attachment.name}:`, errorMessage);
                this.downloadStats.errors++;
                // Продолжаем скачивать остальные файлы
            }
        }
    }

    /**
     * Проверяет, нужно ли скачивать файл по расширению
     */
    private shouldDownloadFile(filename: string): boolean {
        const ext = path.extname(filename).toLowerCase();
        return config.allowedExtensions.includes(ext);
    }

    /**
     * Проверяет, является ли файл архивом
     */
    private isArchive(filename: string): boolean {
        const ext = path.extname(filename).toLowerCase();
        return ['.zip', '.rar', '.7z', '.tar', '.gz'].includes(ext) || filename.toLowerCase().endsWith('.tar.gz');
    }

    /**
     * Скачивает одно вложение с учетом новой структуры папок
     */
    private async downloadSingleAttachment(
        attachment: Attachment, 
        _message: Message, 
        topicDir: string
    ): Promise<void> {
        try {
            // Проверяем расширение файла
            if (!this.shouldDownloadFile(attachment.name)) {
                console.log(`   ⏭️ Пропускаем (неподходящее расширение): ${attachment.name}`);
                this.downloadStats.skippedFiles++;
                return;
            }

            let targetDir = topicDir;
            let fileName = this.sanitizeFileName(attachment.name);
            
            // Если это архив, создаем подпапку Archive
            if (this.isArchive(attachment.name)) {
                targetDir = path.join(topicDir, 'Archive');
                await fs.ensureDir(targetDir);
            }
            
            const filePath = path.join(targetDir, fileName);

            if (await fs.pathExists(filePath)) {
                console.log(`   ⏭️ Пропускаем (уже существует): ${attachment.name}`);
                this.downloadStats.skippedFiles++;
                return;
            }

            console.log(`   ⬇️ Скачиваем: ${attachment.name} (${this.formatFileSize(attachment.size)})`);

            const response: Response = await fetch(attachment.url);
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            const responseBody = response.body;
            if (responseBody === null) {
                throw new Error('Response body is null');
            }

            const writeStream = fs.createWriteStream(filePath);
            
            const readableStream = responseBody as Readable;
            readableStream.pipe(writeStream);

            await new Promise<void>((resolve, reject) => {
                writeStream.on('finish', resolve);
                writeStream.on('error', reject);
                readableStream.on('error', reject);
            });

            console.log(`   ✅ Скачан: ${attachment.name}`);
            this.downloadStats.downloadedFiles++;

            // Если это архив и включена автоматическая распаковка
            if (config.extractArchives && this.isArchive(attachment.name)) {
                await this.extractArchive(filePath, targetDir);
            }

        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            console.error(`   ❌ Ошибка при скачивании ${attachment.name}:`, errorMessage);
            this.downloadStats.errors++;
        }
    }

    /**
     * Извлекает архив и ищет .cs файлы
     */
    private async extractArchive(archivePath: string, outputDir: string): Promise<void> {
        try {
            const archiveName = path.basename(archivePath, path.extname(archivePath));
            const extractDir = path.join(outputDir, `extracted_${archiveName}`);
            await fs.ensureDir(extractDir);

            console.log(`   📦 Распаковываем архив: ${path.basename(archivePath)}`);

            const extension = path.extname(archivePath).toLowerCase();
            
            if (extension === '.zip') {
                await this.extractZip(archivePath, extractDir);
            } else if (extension === '.tar' || archivePath.toLowerCase().includes('.tar')) {
                await this.extractTar(archivePath, extractDir);
            } else if (extension === '.rar') {
                await this.extractRar(archivePath, extractDir);
            } else if (extension === '.7z') {
                console.log(`   ⚠️ 7z архивы пока не поддерживаются: ${path.basename(archivePath)}`);
                return;
            } else {
                console.log(`   ⚠️ Неподдерживаемый формат архива: ${path.basename(archivePath)}`);
                return;
            }

            // Поиск .cs файлов в распакованных файлах
            const csFiles = await this.findCsFiles(extractDir);
            console.log(`   🔍 Найдено .cs файлов в архиве: ${csFiles.length}`);

            // Удаляем архив после успешной распаковки
            await fs.unlink(archivePath);
            console.log(`   🗑️ Удален архив: ${path.basename(archivePath)}`);

        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            console.error(`   ❌ Ошибка при распаковке ${path.basename(archivePath)}:`, errorMessage);
        }
    }

    /**
     * Извлекает ZIP архив
     */
    private async extractZip(zipPath: string, outputDir: string): Promise<void> {
        const fileName = path.basename(zipPath);
        console.log(`   📦 Начинаем распаковку ZIP: ${fileName}`);

        return new Promise((resolve, reject) => {
            const timeoutMs = 5 * 60 * 1000; // 5 минут таймаут
            const timeoutId = setTimeout(() => {
                reject(new Error(`Таймаут распаковки архива ${fileName} (${timeoutMs / 1000}с)`));
            }, timeoutMs);

            yauzl.open(zipPath, { lazyEntries: true }, (err: Error | null, zipfile?: yauzl.ZipFile) => {
                if (err || !zipfile) {
                    clearTimeout(timeoutId);
                    reject(err || new Error('Failed to open zip file'));
                    return;
                }

                let totalEntries = 0;
                let processedEntries = 0;
                let progressDots = 0;
                let lastProgressTime = Date.now();
                
                // Подсчитываем общее количество файлов
                zipfile.on('entry', () => {
                    totalEntries++;
                });

                const progressInterval = setInterval(() => {
                    progressDots = (progressDots + 1) % 4;
                    const dots = '.'.repeat(progressDots + 1);
                    const progress = totalEntries > 0 ? ` (${processedEntries}/${totalEntries})` : '';
                    process.stdout.write(`\r   📦 Распаковка ZIP: ${fileName}${progress}${dots.padEnd(4)}`);
                }, 500);

                const cleanup = () => {
                    clearTimeout(timeoutId);
                    clearInterval(progressInterval);
                    console.log(`\n   ✅ ZIP распакован: ${fileName}`);
                };

                zipfile.readEntry();
                zipfile.on('entry', (entry: yauzl.Entry) => {
                    // Обновляем таймаут при активности
                    if (Date.now() - lastProgressTime > 30000) { // 30 секунд
                        clearTimeout(timeoutId);
                        setTimeout(() => {
                            reject(new Error(`Таймаут распаковки архива ${fileName} (${timeoutMs / 1000}с)`));
                        }, timeoutMs);
                        lastProgressTime = Date.now();
                    }

                    if (/\/$/.test(entry.fileName)) {
                        // Директория
                        processedEntries++;
                        zipfile.readEntry();
                    } else {
                        // Файл
                        zipfile.openReadStream(entry, (streamErr: Error | null, readStream?: NodeJS.ReadableStream) => {
                            if (streamErr || !readStream) {
                                cleanup();
                                reject(streamErr || new Error('Failed to create read stream'));
                                return;
                            }

                            const outputPath = path.join(outputDir, entry.fileName);
                            fs.ensureDir(path.dirname(outputPath)).then(() => {
                                const writeStream = fs.createWriteStream(outputPath);
                                readStream.pipe(writeStream);
                                writeStream.on('close', () => {
                                    processedEntries++;
                                    zipfile.readEntry();
                                });
                                writeStream.on('error', (error) => {
                                    cleanup();
                                    reject(error);
                                });
                            }).catch((error) => {
                                cleanup();
                                reject(error);
                            });
                        });
                    }
                });

                zipfile.on('end', () => {
                    cleanup();
                    resolve();
                });
                
                zipfile.on('error', (error) => {
                    cleanup();
                    reject(error);
                });
            });
        });
    }

    /**
     * Извлекает TAR архив
     */
    private async extractTar(tarPath: string, outputDir: string): Promise<void> {
        await tar.extract({
            file: tarPath,
            cwd: outputDir
        });
    }

    /**
     * Извлекает RAR архив через системную команду unrar
     */
    private async extractRar(rarPath: string, outputDir: string): Promise<void> {
        try {
            // Проверяем наличие unrar
            const { spawn } = require('child_process');
            const { promisify } = require('util');
            const execAsync = promisify(require('child_process').exec);
            
            // Проверяем наличие unrar в системе
            try {
                await execAsync('which unrar');
            } catch {
                console.log(`   ⚠️ unrar не установлен, пропускаем RAR архив: ${path.basename(rarPath)}`);
                return;
            }
            
            // Извлекаем RAR архив с прогресс-индикатором и таймаутом
            const fileName = path.basename(rarPath);
            console.log(`   📦 Начинаем распаковку RAR: ${fileName}`);
            
            // Создаем процесс с таймаутом
            const timeoutMs = 5 * 60 * 1000; // 5 минут таймаут
            const controller = new AbortController();
            const timeoutId = setTimeout(() => {
                controller.abort();
            }, timeoutMs);

            try {
                await new Promise<void>((resolve, reject) => {
                    const unrarProcess = spawn('unrar', ['x', rarPath, `${outputDir}/`], {
                        signal: controller.signal,
                        stdio: ['ignore', 'pipe', 'pipe']
                    });

                    let progressDots = 0;
                    const progressInterval = setInterval(() => {
                        progressDots = (progressDots + 1) % 4;
                        const dots = '.'.repeat(progressDots + 1);
                        process.stdout.write(`\r   📦 Распаковка RAR: ${fileName}${dots.padEnd(4)}`);
                    }, 500);

                    unrarProcess.stdout?.on('data', (data: Buffer) => {
                        // Можно обрабатывать вывод unrar для более детального прогресса
                        const output = data.toString();
                        if (output.includes('Extracting')) {
                            progressDots = 0; // Сброс анимации при активности
                        }
                    });

                    unrarProcess.on('close', (code: number | null) => {
                        clearInterval(progressInterval);
                        clearTimeout(timeoutId);
                        console.log(`\n   ✅ RAR распакован: ${fileName}`);
                        
                        if (code === 0) {
                            resolve();
                        } else {
                            reject(new Error(`unrar завершился с кодом ${code}`));
                        }
                    });

                    unrarProcess.on('error', (error: Error) => {
                        clearInterval(progressInterval);
                        clearTimeout(timeoutId);
                        console.log(`\n   ❌ Ошибка процесса unrar: ${fileName}`);
                        reject(error);
                    });

                    controller.signal.addEventListener('abort', () => {
                        clearInterval(progressInterval);
                        console.log(`\n   ⏱️ Таймаут распаковки RAR: ${fileName}`);
                        unrarProcess.kill('SIGKILL');
                        reject(new Error(`Таймаут распаковки архива ${fileName} (${timeoutMs / 1000}с)`));
                    });
                });
            } finally {
                clearTimeout(timeoutId);
            }
            
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            console.error(`   ❌ Ошибка при распаковке RAR: ${errorMessage}`);
            throw error;
        }
    }

    /**
     * Ищет все .cs файлы в директории рекурсивно
     */
    private async findCsFiles(dir: string): Promise<string[]> {
        const csFiles: string[] = [];
        const items = await fs.readdir(dir, { withFileTypes: true });

        for (const item of items) {
            const fullPath = path.join(dir, item.name);
            if (item.isDirectory()) {
                const subFiles = await this.findCsFiles(fullPath);
                csFiles.push(...subFiles);
            } else if (item.isFile() && path.extname(item.name).toLowerCase() === '.cs') {
                csFiles.push(fullPath);
            }
        }

        return csFiles;
    }

    /**
     * Очищает имя файла от недопустимых символов
     */
    private sanitizeFileName(fileName: string): string {
        return fileName
            .replace(/[<>:"/\\|?*]/gu, '_')
            .replace(/\s+/gu, '_')
            .substring(0, 200);
    }

    /**
     * Форматирует размер файла для отображения
     */
    private formatFileSize(bytes: number | null): string {
        if (bytes === null || bytes === undefined || bytes <= 0) {
            return '0 B';
        }
        
        const sizes = ['B', 'KB', 'MB', 'GB'] as const;
        const i = Math.floor(Math.log(bytes) / Math.log(1024));
        const sizeIndex = Math.min(i, sizes.length - 1);
        const size = sizes[sizeIndex];
        
        if (size === undefined) {
            return '0 B';
        }
        
        return `${(bytes / Math.pow(1024, sizeIndex)).toFixed(1)} ${size}`;
    }

    /**
     * Безопасно извлекает сообщение об ошибке
     */
    private getErrorMessage(error: unknown): string {
        if (error instanceof Error) {
            return error.message;
        }
        if (typeof error === 'string') {
            return error;
        }
        return String(error);
    }

    /**
     * Пауза выполнения на указанное количество миллисекунд
     */
    private sleep(ms: number): Promise<void> {
        return new Promise(resolve => setTimeout(resolve, ms));
    }

    /**
     * Выводит финальную статистику загрузки
     */
    private printFinalStats(): void {
        const totalProcessed = this.downloadStats.processedTopics + this.downloadStats.skippedTopics;
        
        let workTime = '';
        if (this.progressState?.startTime) {
            const elapsed = new Date().getTime() - new Date(this.progressState.startTime).getTime();
            const hours = Math.floor(elapsed / 3600000);
            const minutes = Math.floor((elapsed % 3600000) / 60000);
            workTime = `${hours}ч ${minutes}м`;
        }
        
        console.log('\n' + '='.repeat(60));
        console.log('🎆 ПОЛНАЯ ОБРАБОТКА ФОРУМА ЗАВЕРШЕНА!');
        console.log('='.repeat(60));
        console.log(`⏱️ Время работы: ${workTime}`);
        console.log(`📂 Всего топиков: ${totalProcessed}`);
        console.log(`✅ Обработано топиков: ${this.downloadStats.processedTopics}`);
        console.log(`⏭️ Пропущено топиков: ${this.downloadStats.skippedTopics}`);
        console.log(`💾 Скачано файлов: ${this.downloadStats.downloadedFiles}`);
        console.log(`⏭️ Пропущено файлов: ${this.downloadStats.skippedFiles}`);
        console.log(`❌ Ошибок: ${this.downloadStats.errors}`);
        console.log(`📁 Папка загрузок: ${path.resolve(config.downloadDir, 'topics')}`);
        console.log('='.repeat(60));
        console.log('🎆 МОЖНО ПРОСМАТРИВАТЬ РЕЗУЛЬТАТЫ!');
        console.log('='.repeat(60));
    }
}