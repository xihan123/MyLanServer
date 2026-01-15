import {ref} from 'vue';
import {useAsyncState} from '@vueuse/core';
import {fetchWithTimeout, getSafeErrorMessage, sanitizeFilename} from '../utils/api';

export function useTemplateDownload(slug: string) {
    const password = ref('');

    const {execute, isLoading, error} = useAsyncState(
        async () => {
            const headers: Record<string, string> = {};
            if (password.value) headers['X-Password'] = password.value;

            const response = await fetchWithTimeout(`/api/template/${slug}`, {headers});
            if (!response.ok) {
                const data = await response.json();
                throw new Error(getSafeErrorMessage(data, response.status));
            }

            const blob = await response.blob();
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;

            const contentDisposition = response.headers.get('Content-Disposition');
            let fileName = '模板.xlsx';
            if (contentDisposition) {
                const match = contentDisposition.match(/filename\*=UTF-8''(.+)/);
                if (match?.[1]) fileName = decodeURIComponent(match[1]);
            }
            a.download = sanitizeFilename(fileName);
            a.click();
            window.URL.revokeObjectURL(url);
        },
        null,
        {immediate: false}
    );

    return {password, isLoading, error, download: execute};
}
