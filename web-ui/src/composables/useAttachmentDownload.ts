import {ref} from 'vue';
import {fetchWithTimeout, formatDate, sanitizeFilename} from '../utils/api';

export interface TaskAttachment {
    id: string;
    fileName: string;
    displayName: string;
    fileSize: number;
    uploadDate: string;
}

export interface AttachmentsResponse {
    attachments: TaskAttachment[];
    attachmentDownloadDescription: string | null;
}

export function useAttachmentDownload(slug: string, password: () => string) {
    const attachments = ref<TaskAttachment[]>([]);
    const attachmentDownloadDescription = ref<string | null>(null);
    const isLoading = ref(false);
    const error = ref('');

    const loadAttachments = async () => {
        isLoading.value = true;
        error.value = '';

        try {
            const headers: Record<string, string> = {};
            const pwd = password();
            if (pwd) headers['X-Password'] = pwd;

            const response = await fetchWithTimeout(`/api/distribution/${slug}/attachments`, {headers});

            if (!response.ok) {
                throw new Error('加载附件失败');
            }

            const data: AttachmentsResponse = await response.json();
            attachments.value = data.attachments || [];
            attachmentDownloadDescription.value = data.attachmentDownloadDescription || null;
        } catch (err: any) {
            error.value = err.message || '加载附件失败';
            console.error('加载附件失败:', err);
        } finally {
            isLoading.value = false;
        }
    };

    const downloadAttachment = async (attachmentId: string) => {
        try {
            const headers: Record<string, string> = {};
            const pwd = password();
            if (pwd) headers['X-Password'] = pwd;

            const response = await fetchWithTimeout(`/api/distribution/${slug}/attachments/${attachmentId}`, {headers});

            if (!response.ok) {
                throw new Error('下载附件失败');
            }

            // 获取文件名
            const contentDisposition = response.headers.get('Content-Disposition');
            let filename = 'attachment';
            if (contentDisposition) {
                // 优先使用 RFC 5987 编码的文件名（支持中文）
                const filenameStarMatch = contentDisposition.match(/filename\*=UTF-8''(.+)/);
                if (filenameStarMatch && filenameStarMatch[1]) {
                    filename = decodeURIComponent(filenameStarMatch[1]);
                } else {
                    // 回退到普通文件名
                    const filenameMatch = contentDisposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/);
                    if (filenameMatch && filenameMatch[1]) {
                        filename = filenameMatch[1].replace(/['"]/g, '');
                    }
                }
            }

            // 创建下载链接
            const blob = await response.blob();
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = sanitizeFilename(filename);
            document.body.appendChild(a);
            a.click();

            // 延迟释放 URL，确保下载开始
            setTimeout(() => {
                window.URL.revokeObjectURL(url);
                document.body.removeChild(a);
            }, 100);
        } catch (err: any) {
            error.value = err.message || '下载附件失败';
            console.error('下载附件失败:', err);
        }
    };

    const formattedFileSize = (bytes: number): string => {
        if (bytes === 0) return '0 B';
        const k = 1024;
        const sizes = ['B', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + ' ' + sizes[i];
    };

    return {
        attachments,
        attachmentDownloadDescription,
        isLoading,
        error,
        loadAttachments,
        downloadAttachment,
        formattedFileSize,
        formatDate
    };
}
