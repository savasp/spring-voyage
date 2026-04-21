import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import type { TenantTreeResponse } from "./types";
import { validateTenantTreeResponse } from "./validate-tenant-tree";

describe("validateTenantTreeResponse", () => {
  let errorSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    errorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });
  afterEach(() => {
    errorSpy.mockRestore();
  });

  it("passes a well-formed tree through unchanged", () => {
    const wire: TenantTreeResponse = {
      tree: {
        id: "tenant://acme",
        name: "Acme",
        kind: "Tenant",
        status: "running",
        children: [
          {
            id: "engineering",
            name: "Engineering",
            kind: "Unit",
            status: "paused",
            children: [
              {
                id: "ada",
                name: "Ada",
                kind: "Agent",
                status: "running",
                role: "reviewer",
                primaryParentId: "engineering",
              },
            ],
          },
        ],
      },
    };

    const validated = validateTenantTreeResponse(wire);

    expect(validated.kind).toBe("Tenant");
    expect(validated.children?.[0].kind).toBe("Unit");
    expect(validated.children?.[0].status).toBe("paused");
    expect(validated.children?.[0].children?.[0].role).toBe("reviewer");
    expect(validated.children?.[0].children?.[0].primaryParentId).toBe(
      "engineering",
    );
    expect(errorSpy).not.toHaveBeenCalled();
  });

  it("coerces unknown kind to Unit and logs once", () => {
    const wire = {
      tree: {
        id: "root",
        name: "Root",
        kind: "Tenant",
        status: "running",
        children: [
          {
            id: "mystery",
            name: "Mystery",
            // Simulating a drifted server payload.
            kind: "Swarm",
            status: "running",
          },
        ],
      },
    } as TenantTreeResponse;

    const validated = validateTenantTreeResponse(wire);

    expect(validated.children?.[0].kind).toBe("Unit");
    expect(errorSpy).toHaveBeenCalledTimes(1);
    expect(errorSpy.mock.calls[0][0]).toMatch(/unexpected kind/i);
  });

  it("coerces unknown status to error so drift paints red, not green", () => {
    const wire = {
      tree: {
        id: "root",
        name: "Root",
        kind: "Tenant",
        // Unknown status.
        status: "mystery",
      },
    } as TenantTreeResponse;

    const validated = validateTenantTreeResponse(wire);

    expect(validated.status).toBe("error");
    expect(errorSpy).toHaveBeenCalledTimes(1);
    expect(errorSpy.mock.calls[0][0]).toMatch(/unexpected status/i);
  });

  it("maps null optional fields to undefined so consumers narrow cleanly", () => {
    const wire: TenantTreeResponse = {
      tree: {
        id: "root",
        name: "Root",
        kind: "Tenant",
        status: "running",
        desc: null,
        cost24h: null,
        msgs24h: null,
        role: null,
        skills: null,
        primaryParentId: null,
        children: null,
      },
    };

    const validated = validateTenantTreeResponse(wire);

    expect(validated.desc).toBeUndefined();
    expect(validated.cost24h).toBeUndefined();
    expect(validated.msgs24h).toBeUndefined();
    expect(validated.role).toBeUndefined();
    expect(validated.skills).toBeUndefined();
    expect(validated.primaryParentId).toBeUndefined();
    expect(validated.children).toBeUndefined();
  });

  it("walks nested invalid nodes and reports a path for each coercion", () => {
    const wire = {
      tree: {
        id: "root",
        name: "Root",
        kind: "Tenant",
        status: "running",
        children: [
          {
            id: "u1",
            name: "U1",
            kind: "Unit",
            status: "running",
            children: [
              {
                id: "deep",
                name: "Deep",
                // Drifted value deep in the tree.
                kind: "Mystery",
                status: "running",
              },
            ],
          },
        ],
      },
    } as TenantTreeResponse;

    validateTenantTreeResponse(wire);

    expect(errorSpy).toHaveBeenCalledTimes(1);
    expect(errorSpy.mock.calls[0][1]).toMatchObject({
      path: "$/0/0",
      kind: "Mystery",
    });
  });
});
