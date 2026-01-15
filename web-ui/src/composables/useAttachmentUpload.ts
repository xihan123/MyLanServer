import {computed, ref} from 'vue';
import {formatFileSize} from '../utils/api';

export interface AttachmentFile {
    file: File;
    id: string;
}

// 允许的 MIME 类型映射
const MIME_TYPE_MAP: Record<string, string[]> = {
    '.pdf': ['application/pdf'],
    '.doc': ['application/msword'],
    '.docx': ['application/vnd.openxmlformats-officedocument.wordprocessingml.document'],
    '.xls': ['application/vnd.ms-excel', 'application/msexcel'],
    '.xlsx': ['application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'],
    '.txt': ['text/plain'],
    '.jpg': ['image/jpeg'],
    '.jpeg': ['image/jpeg'],
    '.png': ['image/png'],
    '.gif': ['image/gif'],
    '.bmp': ['image/bmp'],
    '.zip': ['application/zip', 'application/x-zip-compressed'],
    '.rar': ['application/x-rar-compressed'],
    '.7z': ['application/x-7z-compressed']
};

export function useAttachmentUpload() {
    const selectedFiles = ref<AttachmentFile[]>([]);
    const allowedExtensions = ref<string[]>([
        '.pdf', '.doc', '.docx', '.xls', '.xlsx', '.txt',
        '.jpg', '.jpeg', '.png', '.gif', '.bmp',
        '.zip', '.rar', '.7z'
    ]);

    const MAX_FILE_SIZE = 50 * 1024 * 1024; // 50MB

    const isValid = computed(() => selectedFiles.value.length > 0);

    // 验证文件 MIME 类型
    const validateMimeType = (file: File, ext: string): boolean => {
        const allowedMimeTypes = MIME_TYPE_MAP[ext];
        // 如果没有定义允许的 MIME 类型，拒绝该文件类型
        if (!allowedMimeTypes) return false;

        return allowedMimeTypes.includes(file.type);
    };

    const selectFiles = (files: FileList | null) => {
        if (!files || files.length === 0) return;

        for (let i = 0; i < files.length; i++) {
            const file = files[i];
            if (!file) continue;

            // 验证文件大小
            if (file.size > MAX_FILE_SIZE) {
                throw new Error(`文件 "${file.name}" 超过 50MB 限制`);
            }

            // 验证文件扩展名
            const ext = file.name.toLowerCase().substring(file.name.lastIndexOf('.'));
            if (!allowedExtensions.value.includes(ext)) {
                throw new Error(`文件 "${file.name}" 的类型不被允许`);
            }

            // 验证 MIME 类型
            if (!validateMimeType(file, ext)) {
                throw new Error(`文件 "${file.name}" 的内容类型与扩展名不匹配`);
            }

            // 检查是否已存在同名文件
            const exists = selectedFiles.value.some(f => f.file.name === file.name);
            if (exists) {
                throw new Error(`文件 "${file.name}" 已存在`);
            }

            selectedFiles.value.push({
                file,
                // 使用时间戳和随机数生成唯一 ID，避免快速操作时重复
                id: `${Date.now()}-${Math.random().toString(36).substring(2, 9)}`
            });
        }
    };

    const removeFile = (id: string) => {
        const index = selectedFiles.value.findIndex(f => f.id === id);
        if (index > -1) {
            selectedFiles.value.splice(index, 1);
        }
    };

    const reset = () => {
        selectedFiles.value = [];
    };

    const updateAllowedExtensions = (extensions: string[]) => {
        if (extensions && extensions.length > 0) {
            allowedExtensions.value = extensions;
        }
    };

    const getAcceptAttribute = () => {
        return allowedExtensions.value.join(',');
    };

    const getUploadHint = () => {
        const extNames = allowedExtensions.value.map(ext => ext.replace('.', '').toUpperCase()).join('、');
        return `支持 ${extNames} 文件类型，最大 50MB，可多选`;
    };

    return {
        selectedFiles,
        isValid,
        selectFiles,
        removeFile,
        reset,
        updateAllowedExtensions,
        getAcceptAttribute,
        getUploadHint,
        formatFileSize
    };
}
