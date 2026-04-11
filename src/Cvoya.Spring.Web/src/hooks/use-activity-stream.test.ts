import { renderHook, act } from "@testing-library/react";
import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { useActivityStream } from "./use-activity-stream";

class MockEventSource {
  static instances: MockEventSource[] = [];
  onopen: ((ev: Event) => void) | null = null;
  onmessage: ((ev: MessageEvent) => void) | null = null;
  onerror: ((ev: Event) => void) | null = null;
  url: string;
  closed = false;

  constructor(url: string) {
    this.url = url;
    MockEventSource.instances.push(this);
  }

  close() {
    this.closed = true;
  }

  simulateOpen() {
    this.onopen?.(new Event("open"));
  }

  simulateMessage(data: string) {
    this.onmessage?.(new MessageEvent("message", { data }));
  }

  simulateError() {
    this.onerror?.(new Event("error"));
  }
}

describe("useActivityStream", () => {
  beforeEach(() => {
    MockEventSource.instances = [];
    vi.stubGlobal("EventSource", MockEventSource);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("creates an EventSource when enabled", () => {
    renderHook(() => useActivityStream(true));
    expect(MockEventSource.instances).toHaveLength(1);
    expect(MockEventSource.instances[0].url).toBe("/api/v1/activity/stream");
  });

  it("does not create EventSource when disabled", () => {
    renderHook(() => useActivityStream(false));
    expect(MockEventSource.instances).toHaveLength(0);
  });

  it("sets connected to true on open", () => {
    const { result } = renderHook(() => useActivityStream(true));
    expect(result.current.connected).toBe(false);

    act(() => {
      MockEventSource.instances[0].simulateOpen();
    });

    expect(result.current.connected).toBe(true);
  });

  it("accumulates events from messages", () => {
    const { result } = renderHook(() => useActivityStream(true));
    const es = MockEventSource.instances[0];

    act(() => {
      es.simulateMessage(JSON.stringify({
        id: "1",
        timestamp: "2026-01-01T00:00:00Z",
        source: { scheme: "agent", path: "a" },
        eventType: "MessageReceived",
        severity: "Info",
        summary: "First event",
      }));
    });

    expect(result.current.events).toHaveLength(1);
    expect(result.current.events[0].summary).toBe("First event");

    act(() => {
      es.simulateMessage(JSON.stringify({
        id: "2",
        timestamp: "2026-01-01T00:00:01Z",
        source: { scheme: "agent", path: "b" },
        eventType: "MessageSent",
        severity: "Info",
        summary: "Second event",
      }));
    });

    expect(result.current.events).toHaveLength(2);
    // newest first
    expect(result.current.events[0].summary).toBe("Second event");
  });

  it("sets connected to false on error", () => {
    const { result } = renderHook(() => useActivityStream(true));
    const es = MockEventSource.instances[0];

    act(() => es.simulateOpen());
    expect(result.current.connected).toBe(true);

    act(() => es.simulateError());
    expect(result.current.connected).toBe(false);
  });

  it("closes EventSource on unmount", () => {
    const { unmount } = renderHook(() => useActivityStream(true));
    const es = MockEventSource.instances[0];
    expect(es.closed).toBe(false);

    unmount();
    expect(es.closed).toBe(true);
  });
});
