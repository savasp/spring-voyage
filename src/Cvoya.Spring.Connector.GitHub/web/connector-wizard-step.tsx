"use client";

// GitHub connector wizard-step UI. This lives alongside `connector-tab.tsx`
// in the connector package so the .NET connector owns both the post-bind
// management surface (the tab) AND the pre-bind wizard surface (this file).
//
// The two components are deliberately separate (see #199):
//
// * `connector-tab.tsx` — mounted on /units/[id] for an already-bound unit.
//   Loads existing config and installations from the live actor.
// * `connector-wizard-step.tsx` — mounted inside the create-unit wizard
//   before the unit exists. No unit id, no live config — it's a pure form
//   that produces a config payload the wizard bundles into the single
//   create-unit call.
//
// The host web app resolves this file via the `@connector-github/*` path
// alias declared in `src/Cvoya.Spring.Web/tsconfig.json`. It's listed
// alongside `connector-tab.tsx` in `src/Cvoya.Spring.Web/src/connectors/
// registry.ts` so both entry points are statically known at build time.

import { useCallback, useEffect, useState } from "react";
import { Github, Loader2, RefreshCw } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ApiError, api } from "@/lib/api/client";
import type {
  GitHubInstallationResponse,
  UnitGitHubConfigRequest,
} from "@/lib/api/types";

// Documentation anchor we surface in the disabled-with-reason panel so
// operators can self-serve the credential set-up. Kept in one place — if
// the deployment guide moves, only this constant changes.
const GITHUB_APP_DOCS_URL =
  "https://github.com/cvoya-com/spring-voyage/blob/main/docs/guide/deployment.md#optional--connector-credentials";

// Shape of the Problem+JSON the GitHub connector returns when the App
// credentials are not configured at the deployment level (#609 / #1186).
// The actor and the wizard speak the same contract: `disabled: true` plus
// a human-readable `reason`. The wizard turns that into a friendly panel
// instead of leaking the raw RFC 9110 envelope through `err.message`.
interface ConnectorDisabledProblem {
  disabled: true;
  reason: string;
}

function isConnectorDisabledProblem(
  body: unknown,
): body is ConnectorDisabledProblem {
  return (
    typeof body === "object" &&
    body !== null &&
    "disabled" in body &&
    (body as { disabled?: unknown }).disabled === true
  );
}

/**
 * Extracts the disabled-with-reason payload from an {@link ApiError} thrown
 * by the connector-scoped GitHub endpoints. Returns `null` for any other
 * shape — the caller falls back to the generic error path.
 */
function extractDisabledReason(err: unknown): string | null {
  if (!(err instanceof ApiError) || err.status !== 404) {
    return null;
  }
  if (isConnectorDisabledProblem(err.body)) {
    return err.body.reason;
  }
  return null;
}

// Mirror of the event set in connector-tab.tsx. Kept duplicated on purpose
// — changing the set of offered events in one surface shouldn't silently
// change it in the other. The server clamps anything the user picks to the
// connector's known-safe list.
const AVAILABLE_EVENTS: readonly string[] = [
  "issues",
  "pull_request",
  "issue_comment",
  "push",
  "release",
];

export interface GitHubConnectorWizardStepProps {
  /**
   * Fires whenever the form produces a new valid config payload (or `null`
   * when the form is incomplete). The wizard listens to this and stores
   * the latest payload; on Step 5 it bundles it into the create-unit call.
   */
  onChange: (body: UnitGitHubConfigRequest | null) => void;

  /**
   * Initial values for the form — used when the user navigates back to the
   * wizard step after having filled it out once. Optional.
   */
  initialValue?: UnitGitHubConfigRequest | null;
}

/**
 * Wizard-mode GitHub connector configuration. Collects owner / repo /
 * installation / events, validates locally, and bubbles a
 * {@link UnitGitHubConfigRequest} up to the parent wizard.
 */
export function GitHubConnectorWizardStep({
  onChange,
  initialValue,
}: GitHubConnectorWizardStepProps) {
  const [owner, setOwner] = useState(initialValue?.owner ?? "");
  const [repo, setRepo] = useState(initialValue?.repo ?? "");
  const [installationId, setInstallationId] = useState<number | null>(
    initialValue?.appInstallationId == null
      ? null
      : Number(initialValue.appInstallationId),
  );
  const [events, setEvents] = useState<string[]>(
    initialValue?.events ? [...initialValue.events] : [],
  );

  const [installations, setInstallations] = useState<
    GitHubInstallationResponse[] | null
  >(null);
  const [installationsError, setInstallationsError] = useState<string | null>(
    null,
  );
  const [installUrl, setInstallUrl] = useState<string | null>(null);
  // When the connector reports `disabled: true` at the deployment level
  // (no GitHub App credentials configured), we hide the install/refresh
  // affordances entirely and render a remediation panel pointing at the
  // CLI / docs. Drives the friendly path for #1186.
  const [disabledReason, setDisabledReason] = useState<string | null>(null);
  // #1132: tracks an in-flight installations refetch. The Recheck
  // button reads this to disable itself + announce a busy state via
  // `aria-busy`; the existing-installations Refresh button on the
  // installation-picker reuses the same flag so both controls stay
  // coordinated when the user clicks either one.
  const [rechecking, setRechecking] = useState(false);

  // #1132: lifted out of the mount effect so the Recheck button can
  // re-run the fetch without re-mounting the component (and without the
  // monotonic-token gymnastics the previous implementation used). The
  // function is stable across renders because it has no dependencies —
  // all reads come from `api`, all writes go through setState.
  //
  // Note: every setState below happens AFTER an `await`, which is
  // important — it keeps `react-hooks/set-state-in-effect` quiet when
  // the mount effect calls this function (the rule only flags
  // synchronous setState before the first suspension point).
  const fetchInstallations = useCallback(async () => {
    let list: GitHubInstallationResponse[] = [];
    let disabled: string | null = null;
    try {
      list = await api.listGitHubInstallations();
      setInstallations(list);
      setInstallationsError(null);
      setDisabledReason(null);
    } catch (err) {
      // disabled-with-reason is a first-class connector state, not a
      // failure (#1186). Render the remediation panel instead of the
      // raw RFC 9110 envelope.
      disabled = extractDisabledReason(err);
      if (disabled !== null) {
        setDisabledReason(disabled);
        setInstallationsError(null);
      } else {
        const message = err instanceof Error ? err.message : String(err);
        setInstallationsError(message);
        setDisabledReason(null);
      }
      setInstallations([]);
    }
    // Fetch the install URL whenever the empty-state banner will show
    // (either the list came back empty, or the call errored). #599: the
    // previous implementation only fetched on the catch branch, so
    // platforms where the App simply has no installations surfaced a
    // banner with no call-to-action link.
    //
    // Skip the install-URL fetch when the connector is disabled — the
    // endpoint will 404 with the same disabled payload, and there is
    // no install URL to render anyway (the deployment hasn't been
    // wired up to a GitHub App yet).
    if (disabled === null && list.length === 0) {
      try {
        const { url } = await api.getGitHubInstallUrl();
        setInstallUrl(url);
      } catch {
        // Swallow — the banner already tells the user what's wrong.
      }
    }
  }, []);

  // #1132: imperative wrapper for the Recheck button. Lifts the
  // `rechecking` flag around the fetch so the UI can render the
  // spinner / aria-busy state without leaking that concern into the
  // mount-time effect.
  const recheckInstallations = useCallback(async () => {
    setRechecking(true);
    try {
      await fetchInstallations();
    } finally {
      setRechecking(false);
    }
  }, [fetchInstallations]);

  useEffect(() => {
    void fetchInstallations();
  }, [fetchInstallations]);

  // Push validated state up to the wizard on every change. Null when the
  // minimum required fields are missing so the wizard knows not to bundle
  // a partially-filled config.
  useEffect(() => {
    const trimmedOwner = owner.trim();
    const trimmedRepo = repo.trim();
    if (!trimmedOwner || !trimmedRepo) {
      onChange(null);
      return;
    }
    onChange({
      owner: trimmedOwner,
      repo: trimmedRepo,
      appInstallationId: installationId ?? undefined,
      events: events.length > 0 ? events : undefined,
    });
  }, [owner, repo, installationId, events, onChange]);

  const toggleEvent = (e: string) => {
    setEvents((prev) =>
      prev.includes(e) ? prev.filter((x) => x !== e) : [...prev, e],
    );
  };

  return (
    <div className="space-y-4 rounded-md border border-border bg-muted/30 p-4">
      <div className="flex items-center gap-2">
        <Github className="h-4 w-4" />
        <span className="text-sm font-medium">GitHub connector</span>
      </div>

      {disabledReason !== null && (
        <div
          role="alert"
          className="rounded-md border border-info/50 bg-info/15 px-3 py-2 text-sm text-info"
        >
          <p className="font-medium">
            GitHub connector not configured on this deployment.
          </p>
          <p className="mt-1 text-foreground">{disabledReason}</p>
          <p className="mt-2 text-xs text-foreground">
            An operator needs to register a GitHub App and set
            <code className="mx-1 rounded bg-muted px-1 py-0.5 text-[11px]">
              GitHub__AppId
            </code>
            /
            <code className="mx-1 rounded bg-muted px-1 py-0.5 text-[11px]">
              GitHub__PrivateKeyPem
            </code>
            /
            <code className="mx-1 rounded bg-muted px-1 py-0.5 text-[11px]">
              GitHub__WebhookSecret
            </code>
            in <code className="rounded bg-muted px-1 py-0.5 text-[11px]">
              spring.env
            </code>{" "}
            (or run{" "}
            <code className="rounded bg-muted px-1 py-0.5 text-[11px]">
              spring github-app register
            </code>
            ) before this connector can be bound.
          </p>
          <a
            href={GITHUB_APP_DOCS_URL}
            target="_blank"
            rel="noopener noreferrer"
            className="mt-2 inline-flex h-8 items-center gap-1 rounded-md border border-info/60 bg-info/10 px-3 text-sm font-medium text-info transition-colors hover:bg-info/20 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background"
          >
            View deployment guide
          </a>
        </div>
      )}

      {disabledReason === null &&
        installations &&
        installations.length === 0 && (
          <div
            role="alert"
            className="rounded-md border border-warning/50 bg-warning/15 px-3 py-2 text-sm text-warning"
          >
            <p className="font-medium">No GitHub App installations found.</p>
            <p className="mt-1 text-foreground">
              Install the GitHub App on your account or organisation before
              binding this unit.
            </p>
            {/* #1132: Install-app routes the operator off-site to GitHub
                — once they install the App, GitHub redirects them away,
                and they then have to manually return to the wizard. The
                old code never re-fetched, so the panel stayed stuck on
                "No installations" and the operator hit a dead end. The
                Recheck button re-runs the same `list-installations`
                fetch in place; it's announced as `aria-busy` while in
                flight and disabled to avoid double-clicks. */}
            <div className="mt-2 flex flex-wrap items-center gap-2">
              {installUrl && (
                <a
                  href={installUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="inline-flex h-8 items-center gap-1 rounded-md border border-warning/60 bg-warning/10 px-3 text-sm font-medium text-warning transition-colors hover:bg-warning/20 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background"
                >
                  <Github className="h-4 w-4" aria-hidden="true" />
                  Install GitHub App
                </a>
              )}
              <Button
                size="sm"
                variant="outline"
                onClick={() => void recheckInstallations()}
                disabled={rechecking}
                aria-label="Recheck installations"
                aria-busy={rechecking}
                data-testid="github-recheck-installations"
              >
                {rechecking ? (
                  <Loader2
                    className="mr-1 h-4 w-4 animate-spin"
                    aria-hidden="true"
                  />
                ) : (
                  <RefreshCw
                    className="mr-1 h-4 w-4"
                    aria-hidden="true"
                  />
                )}
                {rechecking ? "Rechecking…" : "Recheck installations"}
                {rechecking && (
                  <span className="sr-only">
                    Refreshing GitHub App installations
                  </span>
                )}
              </Button>
            </div>
            {installationsError && (
              <p className="mt-2 text-xs text-muted-foreground">
                ({installationsError})
              </p>
            )}
          </div>
        )}

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <label className="block space-y-1">
          <span className="text-xs text-muted-foreground">
            Repository owner<span className="text-destructive"> *</span>
          </span>
          <Input
            value={owner}
            onChange={(e) => setOwner(e.target.value)}
            placeholder="acme"
            autoComplete="off"
          />
        </label>
        <label className="block space-y-1">
          <span className="text-xs text-muted-foreground">
            Repository name<span className="text-destructive"> *</span>
          </span>
          <Input
            value={repo}
            onChange={(e) => setRepo(e.target.value)}
            placeholder="platform"
            autoComplete="off"
          />
        </label>
      </div>

      {installations && installations.length > 0 && (
        <label className="block space-y-1">
          <span className="text-xs text-muted-foreground">
            App installation
          </span>
          <div className="flex items-center gap-2">
            <select
              className="h-9 flex-1 rounded-md border border-input bg-background px-3 text-sm"
              value={installationId ?? ""}
              onChange={(e) =>
                setInstallationId(
                  e.target.value === "" ? null : Number(e.target.value),
                )
              }
            >
              <option value="">(auto — use platform default)</option>
              {installations.map((i) => (
                <option key={i.installationId} value={i.installationId}>
                  {i.account} ({i.accountType}, {i.repoSelection})
                </option>
              ))}
            </select>
            <Button
              size="sm"
              variant="outline"
              onClick={() => void recheckInstallations()}
              disabled={rechecking}
              aria-label="Refresh installations"
              aria-busy={rechecking}
            >
              {rechecking ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <RefreshCw className="h-4 w-4" />
              )}
            </Button>
          </div>
        </label>
      )}

      <div className="space-y-1">
        <span className="text-xs text-muted-foreground">Webhook events</span>
        <div className="flex flex-wrap gap-2">
          {AVAILABLE_EVENTS.map((e) => {
            const checked = events.includes(e);
            return (
              <label
                key={e}
                className="inline-flex cursor-pointer items-center gap-1 rounded-md border border-border px-2 py-1 text-xs"
              >
                <input
                  type="checkbox"
                  checked={checked}
                  onChange={() => toggleEvent(e)}
                />
                <span>{e}</span>
              </label>
            );
          })}
        </div>
        <span className="block text-[11px] text-muted-foreground">
          Leave empty to use the connector&apos;s default event set.
        </span>
      </div>
    </div>
  );
}
