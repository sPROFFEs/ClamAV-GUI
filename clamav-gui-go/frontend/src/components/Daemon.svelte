<script>
  import { onMount } from 'svelte'
  import {
    GetDashboardData, StartDaemon, StopDaemon, PingDaemon,
    ReloadDatabase, GetDaemonVersion, ShutdownDaemon,
    GetDaemonStats, GetVersionCommands, ScanFolderWithDaemon
  } from '../../wailsjs/go/main/App'

  let running = false
  let busy = false
  let resultText = ''

  onMount(async () => {
    await refresh()
  })

  async function refresh() {
    try {
      const data = await GetDashboardData()
      running = data.isClamDRunning
    } catch (e) {
      console.error(e)
    }
  }

  async function start() {
    busy = true
    resultText = ''
    try {
      const result = await StartDaemon()
      resultText = result
    } catch (e) {
      resultText = 'Error: ' + e
    }
    busy = false
    await refresh()
  }

  async function stop() {
    busy = true
    resultText = ''
    try {
      const result = await StopDaemon()
      resultText = result
    } catch (e) {
      resultText = 'Error: ' + e
    }
    busy = false
    await refresh()
  }

  async function runCommand(fn, label) {
    resultText = ''
    try {
      resultText = await fn()
    } catch (e) {
      resultText = `Error (${label}): ` + e
    }
  }

  async function scanFolder() {
    resultText = ''
    try {
      const result = await ScanFolderWithDaemon()
      if (result) resultText = result
    } catch (e) {
      resultText = 'Error: ' + e
    }
  }

  async function shutdown() {
    if (!confirm('Are you sure you want to shut down the ClamAV daemon?')) return
    resultText = ''
    try {
      resultText = await ShutdownDaemon()
    } catch (e) {
      resultText = 'Error: ' + e
    }
    setTimeout(refresh, 1500)
  }
</script>

<h2>Daemon Control</h2>
<p style="opacity:0.7; margin-bottom:16px">Use these commands to interact with the ClamAV daemon directly.</p>

<div class="status-row">
  <span style="font-weight:600">Status:</span>
  {#if running}
    <span class="badge-ok">Running</span>
  {:else}
    <span class="badge-fail">Stopped</span>
  {/if}
  <button class="btn btn-sm btn-secondary" on:click={refresh}>Refresh</button>
</div>

<div class="btn-row" style="margin:16px 0">
  <button class="btn btn-primary" on:click={start} disabled={running || busy}>Start Daemon</button>
  <button class="btn btn-secondary" on:click={stop} disabled={!running || busy}>Stop Daemon</button>
</div>

{#if busy}
  <div class="busy-indicator">
    <div class="spinner"></div>
    <span>Working...</span>
  </div>
{/if}

<hr class="separator" />

<div class="card">
  <div class="command-list">
    <div class="command-item">
      <button class="btn btn-secondary" on:click={scanFolder} disabled={!running}>Scan Folder with Daemon</button>
      <p>Scans a selected folder through clamd for on-demand daemon testing.</p>
    </div>

    <div class="command-item">
      <button class="btn btn-secondary" on:click={() => runCommand(PingDaemon, 'ping')} disabled={!running}>Ping Daemon</button>
      <p>Sends a PING command. Should respond with PONG.</p>
    </div>

    <div class="command-item">
      <button class="btn btn-secondary" on:click={() => runCommand(ReloadDatabase, 'reload')} disabled={!running}>Reload Database</button>
      <p>Forces the daemon to reload the virus database.</p>
    </div>

    <div class="command-item">
      <button class="btn btn-secondary" on:click={() => runCommand(GetVersionCommands, 'commands')} disabled={!running}>Show Supported Commands</button>
      <p>Asks the daemon to list all supported commands.</p>
    </div>

    <div class="command-item">
      <button class="btn btn-secondary" on:click={() => runCommand(GetDaemonStats, 'stats')} disabled={!running}>Show Stats</button>
      <p>Displays daemon statistics.</p>
    </div>

    <div class="command-item">
      <button class="btn btn-danger" on:click={shutdown} disabled={!running}>Shutdown Daemon</button>
      <p>Instructs the daemon to perform a clean shutdown.</p>
    </div>
  </div>
</div>

{#if resultText}
  <div class="card result-card">
    <h3>Result</h3>
    <pre>{resultText}</pre>
  </div>
{/if}

<style>
  .status-row {
    display: flex;
    align-items: center;
    gap: 12px;
  }

  .btn-row {
    display: flex;
    gap: 8px;
  }

  .busy-indicator {
    display: flex;
    align-items: center;
    gap: 8px;
    margin: 8px 0;
  }

  .spinner {
    width: 16px;
    height: 16px;
    border: 2px solid #313244;
    border-top: 2px solid #89b4fa;
    border-radius: 50%;
    animation: spin 0.8s linear infinite;
  }

  @keyframes spin {
    to { transform: rotate(360deg); }
  }

  .command-list {
    display: flex;
    flex-direction: column;
    gap: 16px;
  }

  .command-item p {
    font-size: 12px;
    opacity: 0.7;
    margin-top: 4px;
  }

  .result-card {
    margin-top: 16px;
  }

  .result-card pre {
    font-size: 12px;
    white-space: pre-wrap;
    word-break: break-all;
    color: #a6adc8;
    margin-top: 8px;
  }
</style>
