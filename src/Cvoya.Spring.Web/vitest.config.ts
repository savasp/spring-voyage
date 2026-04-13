import { defineConfig } from "vitest/config";
import { resolve } from "path";

export default defineConfig({
  // Setting jsx: "automatic" matches the tsconfig "jsx: react-jsx"
  // setting so connector-package TSX files (imported via the
  // @connector-* aliases below) compile under Vitest's esbuild loader.
  // Without this, those files fail with "React is not defined" because
  // esbuild falls back to the classic runtime for files outside the
  // web project's own tsconfig include list.
  esbuild: {
    jsx: "automatic",
  },
  resolve: {
    alias: {
      "@": resolve(__dirname, "./src"),
      "@connectors": resolve(__dirname, "./src/connectors"),
      // Connector-package aliases must stay in sync with tsconfig.json.
      // The web workspace lives at src/Cvoya.Spring.Web; each connector
      // package sits as a sibling under src/Cvoya.Spring.Connector.<Name>/.
      "@connector-github": resolve(
        __dirname,
        "../Cvoya.Spring.Connector.GitHub/web",
      ),
    },
  },
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./src/test/setup.ts"],
  },
});
