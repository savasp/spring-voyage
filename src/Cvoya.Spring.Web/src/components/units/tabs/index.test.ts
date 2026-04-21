import { afterEach, beforeEach, describe, expect, it } from "vitest";

import {
  __resetTabRegistryForTesting,
  lookupTab,
  registerTab,
  registeredTabs,
  tabKey,
} from "./index";

describe("tabs registry (FOUND-tabscaffold)", () => {
  beforeEach(() => __resetTabRegistryForTesting());
  afterEach(() => __resetTabRegistryForTesting());

  it("starts empty so the foundation PR ships zero tab registrations", () => {
    expect(registeredTabs()).toEqual([]);
  });

  it("composes a typed `<kind>.<tab>` key", () => {
    expect(tabKey("Unit", "Overview")).toBe("Unit.Overview");
    expect(tabKey("Agent", "Skills")).toBe("Agent.Skills");
    expect(tabKey("Tenant", "Budgets")).toBe("Tenant.Budgets");
  });

  it("returns null when no component has been registered", () => {
    expect(lookupTab("Unit", "Overview")).toBeNull();
  });

  it("allows registering a per-(kind, tab) component", () => {
    const Overview = () => null;
    registerTab("Unit", "Overview", Overview);
    expect(lookupTab("Unit", "Overview")).toBe(Overview);
    expect(registeredTabs()).toContain("Unit.Overview");
  });

  it("keeps registrations isolated per (kind, tab) pair", () => {
    const UnitMessages = () => null;
    const AgentMessages = () => null;
    registerTab("Unit", "Messages", UnitMessages);
    registerTab("Agent", "Messages", AgentMessages);
    expect(lookupTab("Unit", "Messages")).toBe(UnitMessages);
    expect(lookupTab("Agent", "Messages")).toBe(AgentMessages);
    expect(lookupTab("Tenant", "Activity")).toBeNull();
  });

  it("ignores duplicate registrations for the same key", () => {
    const First = () => null;
    const Second = () => null;
    registerTab("Unit", "Overview", First);
    registerTab("Unit", "Overview", Second);
    expect(lookupTab("Unit", "Overview")).toBe(First);
  });
});
