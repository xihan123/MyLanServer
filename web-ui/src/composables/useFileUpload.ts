import {computed, ref} from 'vue';
import {useAsyncState} from '@vueuse/core';

export interface UploadForm {
    name: string;
    contact: string;
    department: string;
    password: string;
}

// 允许的 MIME 类型映射
const MIME_TYPE_MAP: Record<string, string[]> = {
    '.xlsx': ['application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'],
    '.xls': ['application/vnd.ms-excel', 'application/msexcel']
};

export function useFileUpload(slug: string) {
    const selectedFile = ref<File | null>(null);
    const form = ref<UploadForm>({name: '', contact: '', department: '', password: ''});
    const attachmentFiles = ref<File[]>([]);
    const uploadProgress = ref(0);

    const isValid = computed(() =>
        selectedFile.value &&
        form.value.name &&
        form.value.contact &&
        form.value.department
    );

    // 验证文件 MIME 类型
    const validateMimeType = (file: File, ext: string): boolean => {
        const allowedMimeTypes = MIME_TYPE_MAP[ext];
        if (!allowedMimeTypes) return true; // 如果没有定义允许的 MIME 类型，跳过验证

        return allowedMimeTypes.includes(file.type);
    };

    const uploadWithProgress = async (): Promise<any> => {
        if (!selectedFile.value) throw new Error('请选择文件');

        // 验证文件 MIME 类型
        const ext = selectedFile.value.name.toLowerCase().substring(selectedFile.value.name.lastIndexOf('.'));
        if (!validateMimeType(selectedFile.value, ext)) {
            throw new Error(`文件 "${selectedFile.value.name}" 的内容类型与扩展名不匹配`);
        }

        return new Promise((resolve, reject) => {
            const xhr = new XMLHttpRequest();
            xhr.open('POST', `/api/submit/${slug}`, true);

            // 监听上传进度
            xhr.upload.addEventListener('progress', (e) => {
                if (e.lengthComputable) {
                    const percentComplete = Math.round((e.loaded / e.total) * 100);
                    uploadProgress.value = percentComplete;
                }
            });

            xhr.addEventListener('load', () => {
                if (xhr.status >= 200 && xhr.status < 300) {
                    try {
                        const response = JSON.parse(xhr.responseText);
                        resolve(response);
                    } catch (e) {
                        reject(new Error('上传失败'));
                    }
                } else {
                    reject(new Error(`上传失败: ${xhr.status}`));
                }
            });

            xhr.addEventListener('error', () => {
                reject(new Error('网络错误'));
            });

            xhr.addEventListener('timeout', () => {
                reject(new Error('请求超时'));
            });

          // 根据文件大小动态设置超时时间（最小 30 秒，最大 120 秒）
          const fileSize = selectedFile.value?.size || 0;
          const timeoutMs = Math.max(30000, Math.min(120000, Math.ceil(fileSize / (1024 * 1024)) * 10000));
          xhr.timeout = timeoutMs;

            const formData = new FormData();
            formData.append('file', selectedFile.value!);
            formData.append('name', form.value.name);
            formData.append('contact', form.value.contact);
            formData.append('department', form.value.department);
            if (form.value.password) formData.append('password', form.value.password);

            // 添加附件文件
            if (attachmentFiles.value && attachmentFiles.value.length > 0) {
                attachmentFiles.value.forEach((file) => {
                  // File 是 Blob 的子类型，不需要强制转换
                  formData.append('attachments', file);
                });
            }

            xhr.send(formData);
        });
    };

    const {execute, isLoading, error} = useAsyncState(
        uploadWithProgress,
        null,
        {immediate: false, resetOnExecute: false}
    );

    const setAttachmentFiles = (files: File[]) => {
        attachmentFiles.value = files;
    };

    const clearError = () => {
        error.value = '';
    };

    const selectFile = (file: File | null) => {
        if (!file) return;

        if (!file.name.match(/\.(xlsx|xls)$/i)) {
            throw new Error('请选择 Excel 文件（.xlsx 或 .xls）');
        }
        if (file.size > 50 * 1024 * 1024) {
            throw new Error('文件大小不能超过 50MB');
        }
        selectedFile.value = file;
    };

    const reset = () => {
        selectedFile.value = null;
        form.value = {name: '', contact: '', department: '', password: ''};
    };

    return {
        selectedFile,
        form,
        isValid,
        isLoading,
        error,
        selectFile,
        upload: execute,
        reset,
        setAttachmentFiles,
        clearError,
        uploadProgress
    };
}
