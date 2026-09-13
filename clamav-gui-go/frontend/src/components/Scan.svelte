<script>
  import { onMount, onDestroy } from 'svelte'
  import { ScanFolder, ScanFile, CancelScan } from '../../wailsjs/go/main/App'
  import { EventsOn, EventsOff } from '../../wailsjs/runtime/runtime'

  let scanning = false
  let scanResults = []
  let scanSummary = null
  let options = {
    heuristicAlerts: false,
    scanEncrypted: false,
    leaveTemps: false,
    moveToQuarantine: false,
    quarantinePath: '',
  }

  let unsubResult
  let unsubComplete
  let unsubStarted

  onMount(() => {
    unsubStarted = EventsOn('scan:started', (path) => {
      scanning = true
      scanResults = []
      scanSummary = null
    })

    unsubResult = EventsOn('scan:result', (result) => {
      scanResults = [...scanResults, result]
    })

    unsubComplete = EventsOn('scan:complete', (progress) => {
      scanning = false
      if (progress.summary) {
        scanSummary = progress.summary
      }
    })
  })

  onDestroy(() => {
    if (unsubResult) EventsOff('scan:result')
    if (unsubComplete) EventsOff('scan:complete')
    if (unsubStarted) EventsOff('scan:started')
  })

  async function scanFolder() {
    try {
      await ScanFolder(JSON.stringify(options))
    } catch (e) {
      console.error('Scan folder error:', e)
    }
  }

  async function scanFile() {
    try {
      await ScanFile(JSON.stringify(options))
    } catch (e) {
      console.error('Scan file error:', e)
    }
  }

  async function cancelScan() {
    try {
      await CancelScan()
    } catch (e) {
      console.error('Cancel error:', e)
    }
  }

  function statusClass(status) {
    if (!status) return ''
    const upper = status.toUpperCase()
    if (upper.endsWith('FOUND')) return 'infected'
    if (upper.endsWith('OK')) return 'clean'
    if (upper.includes('ERROR')) return 'error'
    return ''
  }
</script>

<div class="scan-layout">
  <div class="scan-controls">
    <h2>Scan Controls</h2>
    <div class="btn-row">
      <button class="btn btn-primary" on:click={scanFolder} disabled={scanning}>Scan Folder...</button>
      <button class="btn btn-primary" on:click={scanFile} disabled={scanning}>Scan File...</button>
      <button class="btn btn-secondary" on:click={cancelScan} disabled={!scanning}>Cancel</button>
    </div>

    <details class="options-panel" open>
      <summary>Advanced Options</summary>
      <div class="options-list">
        <label><input type="checkbox" bind:checked={options.heuristicAlerts} /> Enable Heuristic Alerts</label>
        <label><input type="checkbox" bind:checked={options.scanEncrypted} /> Scan Encrypted Files</label>
        <label><input type="checkbox" bind:checked={options.leaveTemps} /> Do not remove temporary files</label>
        <label><input type="checkbox" bind:checked={options.moveToQuarantine} /> Move infected files to quarantine</label>
        {#if options.moveToQuarantine}
          <input class="input" type="text" bind:value={options.quarantinePath} placeholder="Quarantine Folder Path" style="margin-left:24px" />
        {/if}
      </div>
    </details>
  </div>

  <div class="scan-results">
    {#if scanning}
      <div class="scanning-indicator">
        <div class="spinner"></div>
        <span>Scanning...</span>
      </div>
    {/if}

    <div class="results-list">
      {#each scanResults as result}
        <div class="result-row {statusClass(result.status)}">
          <span class="result-path" title={result.filePath}>
            {result.filePath.length > 60 ? '...' + result.filePath.slice(-57) : result.filePath}
          </span>
          <span class="result-status">{result.status}</span>
        </div>
      {/each}
    </div>

    {#if scanSummary}
      <div class="card summary-card">
        <h3 style="text-align:center; margin-bottom:12px">Scan Summary</h3>
        <div class="summary-grid">
          <div class="summary-item">
            <strong>{scanSummary.scannedDirectories || '0'}</strong> Dirs Scanned
          </div>
          <div class="summary-item">
            <strong>{scanSummary.scannedFiles || '0'}</strong> Files Scanned
          </div>
          <div class="summary-item">
            <strong>{scanSummary.timeTaken || 'N/A'}</strong> Time Taken
          </div>
          <div class="summary-item">
            <strong>{scanSummary.engineVersion || 'N/A'}</strong> Engine
          </div>
          <div class="summary-item">
            <strong>{scanSummary.knownViruses || '0'}</strong> Signatures
          </div>
          <div class="summary-item" class:badge-fail={scanSummary.infectedFiles !== '0'}>
            <strong>{scanSummary.infectedFiles || '0'}</strong> Infected
          </div>
        </div>
      </div>
    {/if}
  </div>
</div>

<style>
  .scan-layout {
    display: grid;
    grid-template-columns: 300px 1fr;
    gap: 24px;
    height: 100%;
  }

  .btn-row {
    display: flex;
    gap: 8px;
    margin-bottom: 16px;
  }

  .options-panel {
    margin-top: 16px;
  }

  .options-panel summary {
    cursor: pointer;
    font-weight: 600;
    margin-bottom: 8px;
  }

  .options-list {
    display: flex;
    flex-direction: column;
    gap: 8px;
  }

  .options-list label {
    display: flex;
    align-items: center;
    gap: 8px;
    font-size: 13px;
  }

  .results-list {
    max-height: 400px;
    overflow-y: auto;
    border: 1px solid #313244;
    border-radius: 6px;
    margin-bottom: 16px;
  }

  .result-row {
    display: flex;
    justify-content: space-between;
    padding: 6px 12px;
    font-size: 13px;
    border-bottom: 1px solid #313244;
  }

  .result-row:last-child {
    border-bottom: none;
  }

  .result-row.infected { color: #f38ba8; }
  .result-row.clean { color: #a6e3a1; }
  .result-row.error { color: #f9e2af; }

  .result-path {
    flex: 1;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    margin-right: 12px;
  }

  .result-status {
    flex-shrink: 0;
    font-weight: 500;
  }

  .scanning-indicator {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 12px;
    margin-bottom: 12px;
    background: #181825;
    border-radius: 6px;
  }

  .spinner {
    width: 20px;
    height: 20px;
    border: 3px solid #313244;
    border-top: 3px solid #89b4fa;
    border-radius: 50%;
    animation: spin 0.8s linear infinite;
  }

  @keyframes spin {
    to { transform: rotate(360deg); }
  }

  .summary-card {
    margin-top: 8px;
  }

  .summary-grid {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 12px;
    text-align: center;
  }

  .summary-item {
    font-size: 13px;
  }

  .summary-item strong {
    display: block;
    font-size: 16px;
    margin-bottom: 2px;
  }
</style>
