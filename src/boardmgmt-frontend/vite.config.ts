// vite.config.ts (project root)
import { defineConfig } from 'vite';
import { fileURLToPath, URL } from 'node:url';

export default defineConfig({
  server: {
    port: 4200,
    proxy: {
      '/api': { target: 'https://localhost:44325', changeOrigin: true, secure: false },
      '/hubs': { target: 'https://localhost:44325', changeOrigin: true, secure: false, ws: true },
    },
  },
  resolve: {
    alias: {
      '@app': fileURLToPath(new URL('./src/app', import.meta.url)),
      '@core': fileURLToPath(new URL('./src/app/core', import.meta.url)),
      '@features': fileURLToPath(new URL('./src/app/features', import.meta.url)),
      '@shared': fileURLToPath(new URL('./src/app/features/shared', import.meta.url)),
      '@env': fileURLToPath(new URL('./src/environments', import.meta.url)),
    },
  },
});
