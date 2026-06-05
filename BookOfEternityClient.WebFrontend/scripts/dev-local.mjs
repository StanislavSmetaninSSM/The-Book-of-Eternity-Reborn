import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const LOOPBACK_HOST = '127.0.0.1';
const BACKEND_URL = 'http://127.0.0.1:8787';
const SHUTDOWN_GRACE_MS = 5000;

const scriptDir = dirname(fileURLToPath(import.meta.url));
const frontendRoot = resolve(scriptDir, '..');
const repoRoot = resolve(frontendRoot, '..');
const backendProject = resolve(repoRoot, 'BookOfEternityClient');
const viteCli = join(frontendRoot, 'node_modules', 'vite', 'bin', 'vite.js');

if (!existsSync(viteCli)) {
  console.error('[dev:local] Missing Vite dependency. Run npm ci --prefix BookOfEternityClient.WebFrontend first.');
  process.exit(1);
}

const children = new Set();
let shuttingDown = false;

function startChild(label, command, args, cwd) {
  console.log(`[dev:local] Starting ${label}: ${command} ${args.join(' ')}`);
  const child = spawn(command, args, {
    cwd,
    stdio: 'inherit',
    shell: false,
  });

  children.add(child);

  child.on('error', (error) => {
    children.delete(child);
    if (shuttingDown) {
      return;
    }

    console.error(`[dev:local] ${label} failed to start: ${error.message}`);
    shutdown(1);
  });

  child.on('exit', (code, signal) => {
    children.delete(child);
    if (shuttingDown) {
      return;
    }

    const exitCode = code ?? (signal === 'SIGINT' || signal === 'SIGTERM' ? 0 : 1);
    console.error(`[dev:local] ${label} exited with ${code === null ? signal : `code ${code}`}.`);
    shutdown(exitCode);
  });

  return child;
}

function shutdown(exitCode) {
  if (shuttingDown) {
    return;
  }

  shuttingDown = true;

  if (children.size === 0) {
    process.exit(exitCode);
  }

  let remaining = children.size;
  const timer = setTimeout(() => {
    for (const child of children) {
      if (!child.killed) {
        child.kill('SIGKILL');
      }
    }
    process.exit(exitCode);
  }, SHUTDOWN_GRACE_MS);
  timer.unref();

  for (const child of children) {
    child.once('exit', () => {
      remaining -= 1;
      if (remaining === 0) {
        clearTimeout(timer);
        process.exit(exitCode);
      }
    });

    if (!child.killed) {
      child.kill('SIGTERM');
    }
  }
}

for (const signal of ['SIGINT', 'SIGTERM']) {
  process.on(signal, () => {
    console.log(`[dev:local] Received ${signal}; stopping local dev servers.`);
    shutdown(0);
  });
}

process.on('exit', () => {
  for (const child of children) {
    if (!child.killed) {
      child.kill('SIGTERM');
    }
  }
});

startChild('C# local web host', 'dotnet', ['run', '--project', backendProject, '--', '--web', '--web-url', BACKEND_URL], repoRoot);
startChild('Vite dev server', process.execPath, [viteCli, '--host', LOOPBACK_HOST], frontendRoot);
