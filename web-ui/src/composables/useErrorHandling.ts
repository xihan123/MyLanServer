import {onUnmounted, ref} from 'vue';

export function useErrorHandling() {
    const error = ref('');
    const success = ref('');
    let timeoutId: ReturnType<typeof setTimeout> | null = null;

    const showError = (message: string, duration = 5000) => {
        error.value = message;
        success.value = '';

        if (timeoutId) {
            clearTimeout(timeoutId);
        }

        timeoutId = window.setTimeout(() => {
            error.value = '';
            timeoutId = null;
        }, duration);
    };

    const showSuccess = (message: string, duration = 5000) => {
        success.value = message;
        error.value = '';

        if (timeoutId) {
            clearTimeout(timeoutId);
        }

        timeoutId = window.setTimeout(() => {
            success.value = '';
            timeoutId = null;
        }, duration);
    };

    const clearError = () => {
        error.value = '';
        if (timeoutId) {
            clearTimeout(timeoutId);
            timeoutId = null;
        }
    };

    const clearSuccess = () => {
        success.value = '';
        if (timeoutId) {
            clearTimeout(timeoutId);
            timeoutId = null;
        }
    };

    const clearAll = () => {
        clearError();
        clearSuccess();
    };

    // 组件卸载时清理 timeout，防止内存泄漏
    onUnmounted(() => {
        if (timeoutId) {
            clearTimeout(timeoutId);
            timeoutId = null;
        }
    });

    return {
        error,
        success,
        showError,
        showSuccess,
        clearError,
        clearSuccess,
        clearAll
    };
}
