import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import path from 'path'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: { '@': path.resolve(__dirname, 'src') }
  },
  server: {
    port: 6060,
    allowedHosts: ['.trycloudflare.com', '.cpolar.io', 'localhost', '127.0.0.1'],
    proxy: {
      '/api': {
        target: 'http://localhost:5174',
        changeOrigin: true
      },
      '/hubs': {
        target: 'http://localhost:5174',
        changeOrigin: true,
        ws: true
      }
    }
  }
})
