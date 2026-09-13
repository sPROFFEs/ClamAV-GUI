<script>
  import { onMount } from 'svelte'
  import {
    GetDashboardData, SelectClamAVPath, InitializeConfig,
    UpdateSignatures, RunHealthCheck,
    ScheduleDailyScan, RemoveScheduledScan, BrowseScheduledPath,
    IsScheduledScanConfigured
  } from '../../wailsjs/go/main/App'

  let configured = false
  let statusText = ''
  let isConfigInitialized = false
  let updating = false
  let updateStatus = ''
  let scheduledScanPath = ''
  let scheduledScanTime = '02:00'
  let isScheduledEnabled = false
  let healthResult = ''

  onMount(async () => {
    await refresh()
  })

  async function refresh() {
    try {
      const data = await GetDashboardData()
      configured = data.isClamAVConfigured
      statusText = data.statusText
      isScheduledEnabled = data.isScheduledScanEnabled
    } catch (e) {
      console.error(e)
    }
  }

  async function selectPath() {
    try {
      const path = await SelectClamAVPath()
      if (path) {
        await refresh()
      }
    } catch (e) {
      alert(e)
    }
  }

  async function initConfig() {
    if (!confirm('This will overwrite your existing freshclam.conf and clamd.conf. Continue?')) return
    try {
      const result = await InitializeConfig()
      alert(result)
      if (!result.startsWith('Error')) {
        isConfigInitialized = true
      }
    } catch (e) {
      alert(e)
    }
  }

  async function updateSigs() {
    updating = true
    updateStatus = 'Downloading...'
    try {
      const result = await UpdateSignatures()
      updateStatus = result.message || 'Done'
    } catch (e) {
      updateStatus = 'Error: ' + e
    }
    updating = false
  }

  async function runHealthCheck() {
    try {
      healthResult = await RunHealthCheck()
    } catch (e) {
      healthResult = 'Error: ' + e
    }
  }

  async function scheduleScan() {
    if (!scheduledScanPath) {
      alert('Set a scheduled scan path first.')
      return
    }
    try {
      const result = await ScheduleDailyScan(scheduledScanPath, scheduledScanTime)
      alert(result)
      isScheduledEnabled = await IsScheduledScanConfigured()
    } catch (e) {
      alert(e)
    }
  }

  async function removeSchedule() {
    try {
      const result = await RemoveScheduledScan()
      alert(result)
      isScheduledEnabled = await IsScheduledScanConfigured()
    } catch (e) {
      alert(e)
    }
  }

  async function browsePath() {
    try {
      const path = await BrowseScheduledPath()
      if (path) scheduledScanPath = path
    } catch (e) {
      console.error(e)
    }
  }
</script>

<h2>Settings</h2>

{#if !configured}
  <div class="unconfigured">
    <h3>ClamAV Not Found</h3>
    <p>Please select the root folder of your ClamAV installation.</p>
    <button class="btn btn-primary" on:click={selectPath} style="margin-top:16px">Select ClamAV Folder...</button>
  </div>
{:else}
  <div class="card" style="margin-bottom:16px">
    <h3>Configuration</h3>
    <p style="margin:8px 0">{statusText}</p>
    <div class="btn-row">
      <button class="btn btn-secondary" on:click={selectPath}>Change Path...</button>
      <button class="btn btn-secondary" on:click={runHealthCheck}>Run Health Check</button>
    </div>
    {#if healthResult}
      <pre class="health-output">{healthResult}</pre>
    {/if}
  </div>

  <hr class="separator" />

  <div class="card" style="margin-bottom:16px">
    <h3>Initialize</h3>
    {#if !isConfigInitialized}
      <p style="margin:8px 0; font-size:13px">
        If this is a fresh ClamAV extraction, you may need to initialize the configuration files.
      </p>
      <button class="btn btn-primary" on:click={initConfig}>Initialize Configuration Files</button>
    {:else}
      <p class="badge-ok">Configuration files initialized.</p>
      <p style="margin:8px 0; font-size:13px">Next, download the virus signature database.</p>
      <button class="btn btn-primary" on:click={updateSigs} disabled={updating}>Download Virus Database</button>
      {#if updating}
        <p style="margin-top:8px; font-style:italic; opacity:0.7">{updateStatus}</p>
      {:else if updateStatus}
        <p style="margin-top:8px; font-weight:600">{updateStatus}</p>
      {/if}
    {/if}
  </div>

  <hr class="separator" />

  <div class="card">
    <h3>Scheduled Scan</h3>
    <p style="margin:8px 0; font-size:13px; opacity:0.8">Configure one daily scan using Windows Task Scheduler.</p>
    <div class="btn-row" style="align-items:flex-end">
      <input class="input" type="text" bind:value={scheduledScanPath} placeholder="Path to scan" style="flex:1" />
      <button class="btn btn-secondary" on:click={browsePath}>Browse</button>
    </div>
    <input class="input" type="text" bind:value={scheduledScanTime} placeholder="Time (HH:mm)" style="width:120px; margin:8px 0" />
    <div class="btn-row">
      <button class="btn btn-primary" on:click={scheduleScan}>Save Daily Schedule</button>
      <button class="btn btn-secondary" on:click={removeSchedule}>Remove Schedule</button>
      {#if isScheduledEnabled}
        <span class="badge-ok">Configured</span>
      {/if}
    </div>
  </div>
{/if}

<style>
  .unconfigured {
    text-align: center;
    padding: 60px;
  }

  .btn-row {
    display: flex;
    gap: 8px;
    align-items: center;
    flex-wrap: wrap;
  }

  .health-output {
    margin-top: 12px;
    padding: 12px;
    background: #1e1e2e;
    border-radius: 4px;
    font-size: 12px;
    white-space: pre-wrap;
    color: #a6adc8;
  }
</style>
