"use client";

import { useEffect, useRef, useState } from "react";
import type { ActivityEvent } from "@/lib/api/types";

const MAX_EVENTS = 200;
const BASE = process.env.NEXT_PUBLIC_API_URL ?? "";

export function useActivityStream(enabled = true) {
  const [events, setEvents] = useState<ActivityEvent[]>([]);
  const [connected, setConnected] = useState(false);
  const eventsRef = useRef<ActivityEvent[]>([]);

  useEffect(() => {
    if (!enabled) return;

    const es = new EventSource(`${BASE}/api/v1/activity/stream`);

    es.onopen = () => setConnected(true);

    es.onmessage = (e) => {
      try {
        const event = JSON.parse(e.data) as ActivityEvent;
        eventsRef.current = [event, ...eventsRef.current].slice(0, MAX_EVENTS);
        setEvents([...eventsRef.current]);
      } catch {
        // Ignore malformed messages
      }
    };

    es.onerror = () => {
      setConnected(false);
      // EventSource auto-reconnects by default
    };

    return () => {
      es.close();
      setConnected(false);
    };
  }, [enabled]);

  return { events, connected };
}
