<script>
  import { onMount } from 'svelte'
  import { GetDashboardData, UpdateSignatures } from '../../wailsjs/go/main/App'

  let data = null
  let loading = true
  let updating = false
  let updateOutput = ''
  let updateStatus = ''

  onMount(async () => {
    await refresh()
  })

  async function refresh() {
    loading = true
    try {
      data = await GetDashboardData()
    } catch (e) {
      console.error('Failed to load dashboard:', e)
    }
    loading = false
  }

  async function updateSigs() {
    updating = true
    updateOutput = ''
    updateStatus = 'Downloading database, this may take a few minutes...'
    try {
      const result = await UpdateSignatures()
      updateStatus = result.message || ''
      updateOutput = result.output || ''
    } catch (e) {
      updateStatus = 'An unexpected error occurred.'
      updateOutput = e.toString()
    }
    updating = false
    await refresh()
  }
</script>

<h2>Dashboard</h2>

{#if loading}
  <p>Loading...</p>
{:else if data}
  <div class="cards-grid">
    <div class="card stat-card">
      <div class="stat-icon">ℹ</div>
      <h3>ClamAV Status</h3>
      <p class="stat-value">{data.statusText}</p>
    </div>

    <div class="card stat-card">
      <div class="stat-icon">🛡</div>
      <h3>Virus Definitions</h3>
      <p class="stat-value">{data.virusDefinitionsVersion || 'N/A'}</p>
      {#if updating}
        <p class="stat-sub updating">Updating...</p>
      {:else}
        <p class="stat-sub">{data.lastUpdateTime ? `Last Update: ${data.lastUpdateTime}` : '(run update)'}</p>
      {/if}
      <button class="btn btn-primary" on:click={updateSigs} disabled={updating} style="margin-top:12px">
        Update Signatures
      </button>
    </div>

    <div class="card stat-card">
      <div class="stat-icon">📊</div>
      <h3>Daemon Stats</h3>
      <p class="stat-value daemon-stats">{data.daemonStats}</p>
    </div>

    <div class="card stat-card">
      <div class="stat-icon">📋</div>
      <h3>Scan History</h3>
      <p class="stat-value">{data.totalScans} Scans Performed</p>
      <p class="stat-sub">{data.totalInfectedFiles} Infections Found</p>
    </div>
  </div>

  {#if updateStatus}
    <div style="margin-top:16px">
      <h3>Update Status</h3>
      <p class:badge-ok={updateStatus.includes('successful') || updateStatus.includes('up-to-date')}
         class:badge-fail={updateStatus.includes('error')}>{updateStatus}</p>
    </div>
  {/if}

  {#if updateOutput}
    <div class="card" style="margin-top:16px; max-height:200px; overflow-y:auto">
      <h3>Update Output</h3>
      <pre class="output-pre">{updateOutput}</pre>
    </div>
  {/if}
{/if}

<style>
  .cards-grid {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: 16px;
  }

  .stat-card {
    text-align: center;
  }

  .stat-icon {
    font-size: 28px;
    margin-bottom: 8px;
  }

  .stat-value {
    font-size: 14px;
    word-break: break-word;
  }

  .stat-sub {
    font-size: 12px;
    opacity: 0.7;
    margin-top: 4px;
  }

  .updating {
    color: #f9e2af;
  }

  .daemon-stats {
    white-space: pre-wrap;
    font-size: 12px;
    text-align: left;
    max-height: 120px;
    overflow-y: auto;
  }

  .output-pre {
    font-size: 12px;
    white-space: pre-wrap;
    word-break: break-all;
    color: #a6adc8;
  }

  @media (max-width: 800px) {
    .cards-grid {
      grid-template-columns: repeat(2, 1fr);
    }
  }
</style>
