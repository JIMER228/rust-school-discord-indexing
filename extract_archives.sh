#!/bin/bash

# Скрипт для распаковки архивов
# Создано: 2025-09-12

LOG_FILE="/home/user/Desktop/DiscrodBot/extraction_log.txt"
STATS_FILE="/home/user/Desktop/DiscrodBot/extraction_stats.txt"
DOWNLOADS_DIR="/home/user/Desktop/DiscrodBot/downloads"

# Инициализация статистики
echo "=== СТАТИСТИКА ОБРАБОТКИ АРХИВОВ ===" > "$STATS_FILE"
echo "Дата начала: $(date)" >> "$STATS_FILE"
echo "" >> "$STATS_FILE"

# Счетчики
total_processed=0
successful_extractions=0
skipped_existing=0
failed_extractions=0
deleted_archives=0

# Функция логирования
log_message() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

# Функция извлечения архива
extract_archive() {
    local archive_path="$1"
    local archive_name=$(basename "$archive_path")
    local archive_dir=$(dirname "$archive_path")
    local base_name="${archive_name%.*}"
    
    # Для .tar.gz убираем двойное расширение
    if [[ "$archive_name" == *.tar.gz ]]; then
        base_name="${archive_name%.tar.gz}"
    fi
    
    local extract_dir="${archive_dir}/extracted_${base_name}"
    
    log_message "Обработка: $archive_path"
    
    # Проверяем, не существует ли уже папка extracted_*
    if [[ -d "$extract_dir" ]]; then
        log_message "ПРОПУСК: Папка $extract_dir уже существует"
        ((skipped_existing++))
        return 0
    fi
    
    # Создаем папку для извлечения
    mkdir -p "$extract_dir"
    
    # Определяем тип архива и извлекаем
    case "$archive_name" in
        *.zip)
            if unzip -q "$archive_path" -d "$extract_dir" 2>/dev/null; then
                log_message "УСПЕХ: ZIP архив $archive_name распакован в $extract_dir"
                ((successful_extractions++))
                rm "$archive_path"
                log_message "УДАЛЕН: Оригинальный архив $archive_path"
                ((deleted_archives++))
            else
                log_message "ОШИБКА: Не удалось распаковать ZIP архив $archive_name"
                ((failed_extractions++))
                rmdir "$extract_dir" 2>/dev/null
            fi
            ;;
        *.rar)
            if unrar x -o+ "$archive_path" "$extract_dir/" >/dev/null 2>&1; then
                log_message "УСПЕХ: RAR архив $archive_name распакован в $extract_dir"
                ((successful_extractions++))
                rm "$archive_path"
                log_message "УДАЛЕН: Оригинальный архив $archive_path"
                ((deleted_archives++))
            else
                log_message "ОШИБКА: Не удалось распаковать RAR архив $archive_name"
                ((failed_extractions++))
                rmdir "$extract_dir" 2>/dev/null
            fi
            ;;
        *.7z)
            if 7z x "$archive_path" -o"$extract_dir" >/dev/null 2>&1; then
                log_message "УСПЕХ: 7Z архив $archive_name распакован в $extract_dir"
                ((successful_extractions++))
                rm "$archive_path"
                log_message "УДАЛЕН: Оригинальный архив $archive_path"
                ((deleted_archives++))
            else
                log_message "ОШИБКА: Не удалось распаковать 7Z архив $archive_name"
                ((failed_extractions++))
                rmdir "$extract_dir" 2>/dev/null
            fi
            ;;
        *.tar.gz|*.tgz)
            if tar -xzf "$archive_path" -C "$extract_dir" 2>/dev/null; then
                log_message "УСПЕХ: TAR.GZ архив $archive_name распакован в $extract_dir"
                ((successful_extractions++))
                rm "$archive_path"
                log_message "УДАЛЕН: Оригинальный архив $archive_path"
                ((deleted_archives++))
            else
                log_message "ОШИБКА: Не удалось распаковать TAR.GZ архив $archive_name"
                ((failed_extractions++))
                rmdir "$extract_dir" 2>/dev/null
            fi
            ;;
    esac
    
    ((total_processed++))
}

# Основной цикл обработки
log_message "=== НАЧАЛО ОБРАБОТКИ АРХИВОВ ==="
log_message "Найдено архивов для обработки: 445"

# Обрабатываем ZIP архивы
log_message "=== ОБРАБОТКА ZIP АРХИВОВ ==="
while IFS= read -r -d '' archive; do
    extract_archive "$archive"
done < <(find "$DOWNLOADS_DIR" -name "*.zip" -print0)

# Обрабатываем RAR архивы
log_message "=== ОБРАБОТКА RAR АРХИВОВ ==="
while IFS= read -r -d '' archive; do
    extract_archive "$archive"
done < <(find "$DOWNLOADS_DIR" -name "*.rar" -print0)

# Обрабатываем 7Z архивы
log_message "=== ОБРАБОТКА 7Z АРХИВОВ ==="
while IFS= read -r -d '' archive; do
    extract_archive "$archive"
done < <(find "$DOWNLOADS_DIR" -name "*.7z" -print0)

# Обрабатываем TAR.GZ архивы
log_message "=== ОБРАБОТКА TAR.GZ АРХИВОВ ==="
while IFS= read -r -d '' archive; do
    extract_archive "$archive"
done < <(find "$DOWNLOADS_DIR" -name "*.tar.gz" -o -name "*.tgz" -print0)

# Финальная статистика
log_message "=== ОБРАБОТКА ЗАВЕРШЕНА ==="
log_message "Всего обработано: $total_processed"
log_message "Успешно распаковано: $successful_extractions"
log_message "Пропущено (уже существуют): $skipped_existing"
log_message "Ошибки распаковки: $failed_extractions"
log_message "Удалено архивов: $deleted_archives"

# Сохраняем статистику в файл
{
    echo "Всего обработано: $total_processed"
    echo "Успешно распаковано: $successful_extractions"
    echo "Пропущено (уже существуют): $skipped_existing"
    echo "Ошибки распаковки: $failed_extractions"
    echo "Удалено архивов: $deleted_archives"
    echo ""
    echo "Дата завершения: $(date)"
} >> "$STATS_FILE"

echo "Подробный лог сохранен в: $LOG_FILE"
echo "Статистика сохранена в: $STATS_FILE"