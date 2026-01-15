import {defineConfig} from 'vite'
import vue from '@vitejs/plugin-vue'
import {resolve} from 'path'

export default defineConfig({
    plugins: [vue()],
    base: '/',
    build: {
        outDir: '../wwwroot',
        emptyOutDir: true,
        rollupOptions: {
            input: {
                task: resolve(__dirname, 'task.html'),
                distribution: resolve(__dirname, 'distribution.html')
            }
        }
    }
})
