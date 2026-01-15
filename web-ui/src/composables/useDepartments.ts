import {useAsyncState} from '@vueuse/core';
import {fetchWithTimeout} from '../utils/api';

export interface Department {
    id: string;
    name: string;
}

export function useDepartments() {
    const {state: departments, isLoading: loading, execute: loadDepartments} = useAsyncState(
        async () => {
            const response = await fetchWithTimeout('/api/departments');
            return response.ok ? await response.json() : [];
        },
        [] as Department[],
        {immediate: false}
    );

    return {departments, loading, loadDepartments};
}
