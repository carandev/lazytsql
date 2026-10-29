// @ts-check
import { defineConfig, passthroughImageService } from 'astro/config';
import starlight from '@astrojs/starlight';

// Published on GitHub Pages at https://carandev.github.io/lazytsql/
export default defineConfig({
  site: 'https://carandev.github.io',
  base: '/lazytsql',
  image: { service: passthroughImageService() },
  integrations: [
    starlight({
      title: 'lazytsql',
      // English at the root (/lazytsql/), Spanish under /lazytsql/es/ with the same paths.
      defaultLocale: 'root',
      locales: {
        root: { label: 'English', lang: 'en' },
        es: { label: 'Español', lang: 'es' },
      },
      description: 'A lazygit-style terminal client for SQL Server, with a headless mode for scripts and coding agents.',
      logo: { src: './src/assets/logo.svg', replacesTitle: false },
      favicon: '/favicon.svg',
      social: [{ icon: 'github', label: 'GitHub', href: 'https://github.com/carandev/lazytsql' }],
      editLink: { baseUrl: 'https://github.com/carandev/lazytsql/edit/main/website/' },
      customCss: ['./src/styles/custom.css'],
      // The 404 page is src/content/docs/404.md.
      disable404Route: true,
      sidebar: [
        { label: 'Start here', translations: { es: 'Empieza aquí' }, items: ['getting-started/installation', 'getting-started/quick-start', 'changelog'] },
        {
          label: 'Using lazytsql',
          translations: { es: 'Uso de lazytsql' },
          items: [
            'guides/tui',
            'guides/connections',
            'guides/writing-queries',
            'guides/results',
            'guides/files',
            'guides/headless',
            'guides/bacpac',
            'guides/activity-log',
            'guides/updates',
          ],
        },
        {
          label: 'Reference',
          translations: { es: 'Referencia' },
          items: ['reference/keys', 'reference/cli', 'reference/json-output', 'reference/configuration', 'reference/troubleshooting'],
        },
        {
          label: 'How it works',
          translations: { es: 'Cómo funciona' },
          items: [
            'internals/architecture',
            'internals/execution',
            'internals/completion',
            'internals/tui',
            'internals/storage',
            'internals/security',
            'internals/build-and-release',
            'internals/development',
          ],
        },
      ],
    }),
  ],
});
