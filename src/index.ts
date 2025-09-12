import { Client, GatewayIntentBits, ChannelType, ForumChannel, type Channel } from 'discord.js';
import { config } from './config.js';
import { ForumDownloader } from './forumDownloader.js';

/**
 * Discord бот для скачивания всех файлов из форум-каналов
 */
class DiscordForumBot {
    private readonly client: Client;
    private readonly downloader: ForumDownloader;

    constructor() {
        this.client = new Client({
            intents: [
                GatewayIntentBits.Guilds,
                GatewayIntentBits.GuildMessages,
                GatewayIntentBits.MessageContent // Критически важно для доступа к attachments
            ]
        });

        this.downloader = new ForumDownloader();
        this.setupEventHandlers();
    }

    private setupEventHandlers(): void {
        this.client.once('ready', this.onReady.bind(this));
        this.client.on('error', this.onError.bind(this));
        this.client.on('warn', this.onWarn.bind(this));
        
        // Graceful shutdown
        process.on('SIGINT', () => {
            void this.shutdown(0);
        });
        process.on('SIGTERM', () => {
            void this.shutdown(0);
        });
    }

    private async onReady(): Promise<void> {
        const clientUser = this.client.user;
        if (clientUser === null) {
            throw new Error('Client user is not available');
        }

        console.log(`✅ Бот ${clientUser.tag} успешно запущен!`);
        console.log(`📁 Папка для загрузок: ${config.downloadDir}`);
        
        try {
            await this.startDownload();
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            console.error('❌ Ошибка при запуске загрузки:', errorMessage);
            await this.shutdown(1);
        }
    }

    private onError(error: Error): void {
        console.error('❌ Критическая ошибка Discord клиента:', error);
    }

    private onWarn(warning: string): void {
        console.warn('⚠️ Предупреждение Discord клиента:', warning);
    }

    private async startDownload(): Promise<void> {
        console.log('🔍 Получаем форум-канал...');
        
        const channel: Channel | null = await this.client.channels.fetch(config.forumChannelId);
        
        if (channel === null) {
            throw new Error(`Канал с ID ${config.forumChannelId} не найден`);
        }

        if (channel.type !== ChannelType.GuildForum) {
            const channelTypeName = ChannelType[channel.type] ?? 'Unknown';
            throw new Error(`Канал ${config.forumChannelId} не является форум-каналом (тип: ${channelTypeName})`);
        }

        const forum = channel as ForumChannel;
        console.log(`📋 Форум найден: "${forum.name}" в гильдии "${forum.guild.name}"`);

        await this.downloader.downloadAllFromForum(forum);
        
        console.log('✅ Загрузка завершена! Завершаем работу бота...');
        await this.shutdown(0);
    }

    private async shutdown(exitCode: number = 0): Promise<void> {
        console.log('\n🔄 Завершение работы бота...');
        
        try {
            if (this.client.isReady()) {
                await this.client.destroy();
                console.log('✅ Discord клиент закрыт');
            }
        } catch (error) {
            console.error('❌ Ошибка при закрытии Discord клиента:', error);
        }
        
        process.exit(exitCode);
    }

    private getErrorMessage(error: unknown): string {
        if (error instanceof Error) {
            return error.message;
        }
        if (typeof error === 'string') {
            return error;
        }
        return String(error);
    }

    public async start(): Promise<void> {
        try {
            console.log('🤖 Запуск Discord бота...');
            await this.client.login(config.token);
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            console.error('❌ Ошибка при запуске бота:', errorMessage);
            await this.shutdown(1);
        }
    }
}

/**
 * Обрабатывает необработанные отклонения промисов
 */
function handleUnhandledRejection(reason: unknown): void {
    console.error('❌ Необработанное отклонение промиса:', reason);
    process.exit(1);
}

/**
 * Обрабатывает необработанные исключения
 */
function handleUncaughtException(error: Error): void {
    console.error('❌ Необработанное исключение:', error);
    process.exit(1);
}

/**
 * Обрабатывает ошибку запуска
 */
function handleStartupError(error: unknown): void {
    const errorMessage = error instanceof Error ? error.message : String(error);
    console.error('❌ Критическая ошибка при запуске:', errorMessage);
    process.exit(1);
}

// Обработка необработанных исключений
process.on('unhandledRejection', handleUnhandledRejection);
process.on('uncaughtException', handleUncaughtException);

// Запуск бота
const bot = new DiscordForumBot();
bot.start().catch(handleStartupError);