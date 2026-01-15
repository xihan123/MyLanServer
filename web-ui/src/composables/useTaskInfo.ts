import {useAsyncState} from '@vueuse/core';
import {computed} from 'vue';
import {fetchWithTimeout, validateSlug, getSafeErrorMessage} from '../utils/api';

export interface TaskInfo {
    id: string;
    slug: string;
    title: string;
    description: string;
    taskType: number;
    hasPassword: boolean;
    maxLimit: number;
    currentCount: number;
    expiryDate: string | null;
    isActive: boolean;
    isExpired: boolean;
    isLimitReached: boolean;
    allowAttachmentUpload: boolean;
    allowedExtensions: string[];
}

export function useTaskInfo(slug: string) {
    const {state: taskInfo, error, isLoading, execute} = useAsyncState(
        async () => {
            if (!validateSlug(slug)) throw new Error('无效的任务标识符');

            const response = await fetchWithTimeout(`/api/task/${slug}/info`);
            if (!response.ok) {
                const errorData = await response.json();
                throw new Error(getSafeErrorMessage(errorData, response.status));
            }
            return await response.json() as TaskInfo;
        },
        null as TaskInfo | null,
        {immediate: true}
    );

    const errorMessage = computed(() => (error.value as Error)?.message || '');

    return {taskInfo, errorMessage, isLoading, reload: execute};
}
