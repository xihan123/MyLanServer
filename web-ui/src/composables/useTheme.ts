import {useLocalStorage, usePreferredDark} from '@vueuse/core';
import {computed, watchEffect} from 'vue';

export function useTheme() {
    const prefersDark = usePreferredDark();
    const savedTheme = useLocalStorage<'light' | 'dark' | null>('theme', null);

    const theme = computed(() => savedTheme.value ?? (prefersDark.value ? 'dark' : 'light'));

    watchEffect(() => {
        document.documentElement.setAttribute('data-theme', theme.value);
    });

    const toggleTheme = () => {
        savedTheme.value = theme.value === 'dark' ? 'light' : 'dark';
    };

    return {theme, toggleTheme};
}
