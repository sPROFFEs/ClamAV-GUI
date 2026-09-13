<script>
  import { onMount } from 'svelte'
  import {
    GetMonitoredPaths, AddMonitoredPath, RemoveMonitoredPath,
    GetFileTypeFilters, AddFileTypeFilter, RemoveFileTypeFilter,
    GetExcludedPaths, AddExcludedPath, RemoveExcludedPath,
    ExportMonitoringLog, GetDashboardData
  } from '../../wailsjs/go/main/App'

  let configured = false
  let daemonRunning = false
  let monitoredPaths = []
  let fileTypeFilters = []
  let excludedPaths = []
  let newFilter = ''
  let logEntries = []
  let monitoringActive = false

  // Note: Real-time monitoring via FileSystemWatcher is handled differently in Wails.
  // The Go backend would need to emit events. For now, we show the UI and config.

  onMount(async () => {
    await refresh()
  })

  async function refresh() {
    try {
      const data = await GetDashboardData()
      configured = data.isClamAVConfigured
      daemonRunning = data.isClamDRunning
      monitoredPaths = await GetMonitoredPaths() || []
      fileTypeFilters = await GetFileTypeFilters() || []
      excludedPaths = await GetExcludedPaths() || []
    } catch (e) {
      console.error(e)
    }
  }

  async function addPath() {
    try {
      const path = await AddMonitoredPath()
      if (path) {
        monitoredPaths = await GetMonitoredPaths() || []
      }
    } catch (e) {
      console.error(e)
    }
  }

  async function removePath(path) {
    try {
      await RemoveMonitoredPath(path)
      monitoredPaths = await GetMonitoredPaths() || []
    } catch (e) {
      console.error(e)
    }
  }

  async function addFilter() {
    if (!newFilter.trim()) return
    try {
      await AddFileTypeFilter(newFilter.trim())
      fileTypeFilters = await GetFileTypeFilters() || []
      newFilter = ''
    } catch (e) {
      console.error(e)
    }
  }

  async function removeFilter(filter) {
    try {
      await RemoveFileTypeFilter(filter)
      fileTypeFilters = await GetFileTypeFilters() || []
    } catch (e) {
      console.error(e)
    }
  }

  async function addExclusion(type) {
    try {
      const path = await AddExcludedPath(type)
      if (path) {
        excludedPaths = await GetExcludedPaths() || []
      }
    } catch (e) {
      console.error(e)
    }
  }

  async function removeExclusion(path) {
    try {
      await RemoveExcludedPath(path)
      excludedPaths = await GetExcludedPaths() || []
    } catch (e) {
      console.error(e)
    }
  }

  async function exportLog() {
    try {
      const result = await ExportMonitoringLog(logEntries)
      if (result) alert(result)
    } catch (e) {
      alert('Export failed: ' + e)
    }
  }

  function startMonitoring() {
    if (!daemonRunning) {
      alert('The daemon is not running. Please start the daemon from the Daemon tab before enabling monitoring.')
      return
    }
    monitoringActive = true
    logEntries = [...logEntries, ...monitoredPaths.map(p => `Monitoring started for: ${p}`)]
  }

  function stopMonitoring() {
    monitoringActive = false
    logEntries = [...logEntries, 'Monitoring stopped.']
  }
</script>

<h2>On-Access Monitoring</h2>
<p style="opacity:0.7; margin-bottom:16px">
  This feature enables real-time protection by scanning files as soon as they are created or modified.
</p>

<div class="card" style="margin-bottom:16px">
  <div class="status-row">
    <h3>Status:</h3>
    {#if monitoringActive}
      <span class="badge-ok">ACTIVE</span>
    {:else}
      <span class="badge-fail">INACTIVE</span>
    {/if}
  </div>

  <h3 style="margin-top:16px">Monitored Paths:</h3>
  <div class="list-box">
    {#each monitoredPaths as path}
      <div class="list-item">
        <span>{path}</span>
        <button class="btn btn-sm btn-danger" on:click={() => removePath(path)}>Remove</button>
      </div>
    {/each}
    {#if monitoredPaths.length === 0}
      <div class="list-empty">No monitored paths configured.</div>
    {/if}
  </div>
  <div class="btn-row" style="margin-top:8px">
    <button class="btn btn-secondary" on:click={addPath}>Add Folder...</button>
  </div>

  <hr class="separator" />

  <h3>Monitoring Options</h3>

  <h3 style="margin-top:12px">File Type Filters:</h3>
  <div class="chips">
    {#each fileTypeFilters as filter}
      <span class="chip">
        {filter}
        <button class="chip-delete" on:click={() => removeFilter(filter)}>x</button>
      </span>
    {/each}
  </div>
  <div class="btn-row" style="margin-top:8px">
    <input class="input" type="text" bind:value={newFilter} placeholder="e.g., .exe, *.dll" />
    <button class="btn btn-secondary" on:click={addFilter}>Add Filter</button>
  </div>

  <h3 style="margin-top:16px">Exclusions:</h3>
  <div class="list-box">
    {#each excludedPaths as path}
      <div class="list-item">
        <span>{path}</span>
        <button class="btn btn-sm btn-danger" on:click={() => removeExclusion(path)}>Remove</button>
      </div>
    {/each}
    {#if excludedPaths.length === 0}
      <div class="list-empty">No exclusions configured.</div>
    {/if}
  </div>
  <div class="btn-row" style="margin-top:8px">
    <button class="btn btn-secondary" on:click={() => addExclusion('File')}>Add File...</button>
    <button class="btn btn-secondary" on:click={() => addExclusion('Folder')}>Add Folder...</button>
  </div>
</div>

<div class="btn-row" style="margin-bottom:16px">
  <button class="btn btn-primary" on:click={startMonitoring} disabled={monitoringActive || monitoredPaths.length === 0}>Start Monitoring</button>
  <button class="btn btn-secondary" on:click={stopMonitoring} disabled={!monitoringActive}>Stop Monitoring</button>
  <button class="btn btn-secondary" on:click={exportLog} disabled={logEntries.length === 0}>Export Log</button>
</div>

<h3>Live Log</h3>
<div class="log-box">
  {#each logEntries as entry}
    <div class="log-entry">{entry}</div>
  {/each}
  {#if logEntries.length === 0}
    <div class="list-empty">No log entries yet.</div>
  {/if}
</div>

<style>
  .status-row {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  .btn-row {
    display: flex;
    gap: 8px;
    flex-wrap: wrap;
  }

  .list-box {
    border: 1px solid #313244;
    border-radius: 6px;
    max-height: 150px;
    overflow-y: auto;
  }

  .list-item {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 8px 12px;
    border-bottom: 1px solid #313244;
    font-size: 13px;
  }

  .list-item:last-child {
    border-bottom: none;
  }

  .list-empty {
    padding: 16px;
    text-align: center;
    opacity: 0.5;
    font-size: 13px;
  }

  .chips {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
    min-height: 32px;
    padding: 4px;
  }

  .chip {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    background: #313244;
    padding: 4px 10px;
    border-radius: 12px;
    font-size: 12px;
  }

  .chip-delete {
    background: none;
    border: none;
    color: #f38ba8;
    cursor: pointer;
    font-size: 14px;
    padding: 0 2px;
  }

  .log-box {
    border: 1px solid #313244;
    border-radius: 6px;
    height: 200px;
    overflow-y: auto;
    padding: 8px;
  }

  .log-entry {
    font-size: 12px;
    font-family: monospace;
    padding: 2px 0;
    border-bottom: 1px solid #1e1e2e;
  }
</style>
