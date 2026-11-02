/// <reference types="vite/client" />
import type { WoundActualShellPackets, createWoundActualShellPackets } from './helpers/woundActualShellFixtures';

// Compile the actual-shell packet factory and its JSON imports; no runtime assertion claim.
export type ActualShellPackets = ReturnType<typeof createWoundActualShellPackets> extends WoundActualShellPackets ? true : never;
export type ActualShellReadRoutes = keyof WoundActualShellPackets['bootstrap'];
