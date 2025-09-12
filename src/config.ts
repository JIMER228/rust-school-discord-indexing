import * as fs from 'fs';
import * as path from 'path';
import type { Config, ProcessEnv } from './types.js';

class ConfigManager implements Config {
    private readonly _token: string;
    private readonly _forumChannelId: string;
    private readonly _downloadDir: string;
    private readonly _maxConcurrentDownloads: number;
    private readonly _requestDelay: number;
    private readonly _allowedExtensions: readonly string[];
    private readonly _extractArchives: boolean;

    constructor() {
        this.loadEnv();
        this.validateConfig();
        
        const env = process.env as ProcessEnv;
        
        // После validateConfig мы знаем, что эти значения точно есть
        this._token = this.assertString(env.DISCORD_TOKEN, 'DISCORD_TOKEN');
        this._forumChannelId = this.assertString(env.FORUM_CHANNEL_ID, 'FORUM_CHANNEL_ID');
        this._downloadDir = env.DOWNLOAD_DIR ?? 'downloads';
        this._maxConcurrentDownloads = this.parsePositiveInt(env.MAX_CONCURRENT_DOWNLOADS, 5, 'MAX_CONCURRENT_DOWNLOADS');
        this._requestDelay = this.parsePositiveInt(env.REQUEST_DELAY, 1000, 'REQUEST_DELAY');
        this._allowedExtensions = this.parseExtensions(env.ALLOWED_EXTENSIONS, '.cs,.zip,.rar,.7z,.tar,.tar.gz');
        this._extractArchives = this.parseBoolean(env.EXTRACT_ARCHIVES, true);
    }

    private assertString(value: string | undefined, name: string): string {
        if (typeof value !== 'string' || value.length === 0) {
            throw new Error(`${name} должен быть непустой строкой`);
        }
        return value;
    }

    private parsePositiveInt(value: string | undefined, defaultValue: number, name: string): number {
        if (value === undefined) {
            return defaultValue;
        }
        
        const parsed = parseInt(value, 10);
        if (isNaN(parsed) || parsed <= 0) {
            throw new Error(`${name} должен быть положительным числом`);
        }
        
        return parsed;
    }

    private parseExtensions(value: string | undefined, defaultValue: string): readonly string[] {
        const extensionsString = value ?? defaultValue;
        return extensionsString.split(',').map(ext => ext.trim().toLowerCase());
    }

    private parseBoolean(value: string | undefined, defaultValue: boolean): boolean {
        if (value === undefined) {
            return defaultValue;
        }
        return value.toLowerCase() === 'true';
    }

    private loadEnv(): void {
        const envPath = path.join(process.cwd(), '.env');
        
        if (!fs.existsSync(envPath)) {
            return;
        }

        const envContent = fs.readFileSync(envPath, 'utf8');
        
        for (const line of envContent.split('\n')) {
            const trimmedLine = line.trim();
            if (trimmedLine && !trimmedLine.startsWith('#')) {
                const equalIndex = trimmedLine.indexOf('=');
                if (equalIndex > 0) {
                    const key = trimmedLine.slice(0, equalIndex).trim();
                    const value = trimmedLine.slice(equalIndex + 1).trim();
                    if (key && value) {
                        process.env[key] = value;
                    }
                }
            }
        }
    }

    private validateConfig(): void {
        const env = process.env as ProcessEnv;
        
        if (!env.DISCORD_TOKEN) {
            throw new Error('DISCORD_TOKEN не задан в переменных окружения');
        }
        
        if (!env.FORUM_CHANNEL_ID) {
            throw new Error('FORUM_CHANNEL_ID не задан в переменных окружения');
        }

        // Валидация ID канала
        if (!/^\d{17,19}$/u.test(env.FORUM_CHANNEL_ID)) {
            throw new Error('FORUM_CHANNEL_ID должен быть валидным Discord ID (17-19 цифр)');
        }
    }

    public get token(): string {
        return this._token;
    }

    public get forumChannelId(): string {
        return this._forumChannelId;
    }

    public get downloadDir(): string {
        return this._downloadDir;
    }

    public get maxConcurrentDownloads(): number {
        return this._maxConcurrentDownloads;
    }

    public get requestDelay(): number {
        return this._requestDelay;
    }

    public get allowedExtensions(): readonly string[] {
        return this._allowedExtensions;
    }

    public get extractArchives(): boolean {
        return this._extractArchives;
    }
}

export const config: Config = new ConfigManager();