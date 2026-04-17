// API-client decoration utilities (#440 point 3).
//
// The hosted build attaches bearer tokens and tenant headers to every
// outgoing request. Rather than patch call sites, the OSS shell
// exposes a `withDecorators` helper that wraps a fetch-like callable
// with every decorator the registered extensions supply.
//
// OSS ships no decorators, so in standalone mode this is an identity
// wrap.

import type { ClientDecorator, FetchFn, IAuthContext } from "./types";

/**
 * Compose a list of decorators over an inner fetch-like callable.
 * The first decorator in the list becomes the outermost wrapper:
 * `[A, B, C]` → `A(B(C(inner)))`.
 */
export function withDecorators(
  inner: FetchFn,
  decorators: readonly ClientDecorator[],
): FetchFn {
  return decorators.reduceRight<FetchFn>(
    (acc, decorator) => decorator(acc),
    inner,
  );
}

/**
 * Convenience factory: a decorator that attaches the headers returned
 * by an `IAuthContext`. Hosted supplies its own auth decorator via
 * `registerExtension({ decorators: [...] })`; this factory is the
 * reference implementation and the one the OSS tests exercise.
 */
export function authHeadersDecorator(auth: IAuthContext): ClientDecorator {
  return (inner: FetchFn) => async (input, init) => {
    const extraHeaders = auth.getHeaders();
    if (Object.keys(extraHeaders).length === 0) {
      return inner(input, init);
    }
    const merged = new Headers(init?.headers);
    for (const [key, value] of Object.entries(extraHeaders)) {
      merged.set(key, value);
    }
    return inner(input, { ...init, headers: merged });
  };
}
