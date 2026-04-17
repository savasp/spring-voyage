// Public surface of the portal extension system (#440). See
// `./README.md` for the full contract and usage guide.

export type {
  AuthUser,
  ClientDecorator,
  FetchFn,
  IAuthContext,
  NavSection,
  PaletteAction,
  PortalExtension,
  RouteEntry,
  ShellSlot,
} from "./types";
export { NAV_SECTION_ORDER } from "./types";

export {
  registerExtension,
  computeMergedExtensions,
  type MergedExtensions,
} from "./registry";

export {
  ExtensionProvider,
  useExtensions,
  useRoutes,
  usePaletteActions,
  useAuthContext,
} from "./context";

export { withDecorators, authHeadersDecorator } from "./api";

export { defaultAuthContext, defaultRoutes, defaultActions } from "./defaults";
