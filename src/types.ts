export interface Config {
  readonly token: string;
  readonly forumChannelId: string;
  readonly downloadDir: string;
  readonly maxConcurrentDownloads: number;
  readonly requestDelay: number;
  readonly allowedExtensions: readonly string[];
  readonly extractArchives: boolean;
}

export interface DownloadStats {
  totalFiles: number;
  downloadedFiles: number;
  skippedFiles: number;
  errors: number;
  processedTopics: number;
  skippedTopics: number;
}

export interface TopicInfo {
  name: string;
  messages: TopicMessage[];
  hasRelevantFiles: boolean;
}

export interface TopicMessage {
  id: string;
  author: string;
  content: string;
  timestamp: string;
  attachments: string[];
}

export interface ProgressState {
  lastProcessedIndex: number;
  totalTopics: number;
  processedTopics: string[]; // IDs уже обработанных тредов
  startTime: string;
  lastUpdateTime: string;
  stats: DownloadStats;
}

export interface EnvVariables {
  readonly DISCORD_TOKEN?: string | undefined;
  readonly FORUM_CHANNEL_ID?: string | undefined;
  readonly DOWNLOAD_DIR?: string | undefined;
  readonly MAX_CONCURRENT_DOWNLOADS?: string | undefined;
  readonly REQUEST_DELAY?: string | undefined;
}

export interface ProcessEnv {
  readonly DISCORD_TOKEN: string | undefined;
  readonly FORUM_CHANNEL_ID: string | undefined;
  readonly DOWNLOAD_DIR: string | undefined;
  readonly MAX_CONCURRENT_DOWNLOADS: string | undefined;
  readonly REQUEST_DELAY: string | undefined;
  readonly ALLOWED_EXTENSIONS: string | undefined;
  readonly EXTRACT_ARCHIVES: string | undefined;
  readonly [key: string]: string | undefined;
}