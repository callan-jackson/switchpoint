interface ImportMetaEnv {
  /** 'true' enables the in-browser MSW worker with sample data. */
  readonly VITE_USE_MOCKS?: string
  /** Label rendered in the top-bar environment badge. */
  readonly VITE_ENV_LABEL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
