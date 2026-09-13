<script>
  import Dashboard from './components/Dashboard.svelte'
  import Scan from './components/Scan.svelte'
  import Settings from './components/Settings.svelte'
  import History from './components/History.svelte'
  import Monitoring from './components/Monitoring.svelte'
  import Daemon from './components/Daemon.svelte'
  import Quarantine from './components/Quarantine.svelte'

  let activeTab = 'dashboard'

  const tabs = [
    { id: 'dashboard', label: 'Dashboard' },
    { id: 'scan', label: 'Scan' },
    { id: 'settings', label: 'Settings' },
    { id: 'history', label: 'History' },
    { id: 'monitoring', label: 'Monitoring' },
    { id: 'daemon', label: 'Daemon' },
    { id: 'quarantine', label: 'Quarantine' },
  ]
</script>

<main>
  <nav class="tabs">
    {#each tabs as tab}
      <button
        class="tab-btn"
        class:active={activeTab === tab.id}
        on:click={() => activeTab = tab.id}
      >
        {tab.label}
      </button>
    {/each}
  </nav>

  <div class="tab-content">
    {#if activeTab === 'dashboard'}
      <Dashboard />
    {:else if activeTab === 'scan'}
      <Scan />
    {:else if activeTab === 'settings'}
      <Settings />
    {:else if activeTab === 'history'}
      <History />
    {:else if activeTab === 'monitoring'}
      <Monitoring />
    {:else if activeTab === 'daemon'}
      <Daemon />
    {:else if activeTab === 'quarantine'}
      <Quarantine />
    {/if}
  </div>
</main>

<style>
  :global(*) {
    margin: 0;
    padding: 0;
    box-sizing: border-box;
  }

  :global(body) {
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, sans-serif;
    background: #1e1e2e;
    color: #cdd6f4;
    overflow: hidden;
    height: 100vh;
  }

  :global(#app) {
    height: 100vh;
    display: flex;
    flex-direction: column;
  }

  main {
    display: flex;
    flex-direction: column;
    height: 100vh;
  }

  .tabs {
    display: flex;
    background: #181825;
    border-bottom: 1px solid #313244;
    padding: 0 8px;
    flex-shrink: 0;
  }

  .tab-btn {
    padding: 12px 20px;
    border: none;
    background: transparent;
    color: #a6adc8;
    cursor: pointer;
    font-size: 14px;
    font-weight: 500;
    border-bottom: 2px solid transparent;
    transition: all 0.2s;
  }

  .tab-btn:hover {
    color: #cdd6f4;
    background: #1e1e2e;
  }

  .tab-btn.active {
    color: #89b4fa;
    border-bottom-color: #89b4fa;
  }

  .tab-content {
    flex: 1;
    overflow-y: auto;
    padding: 24px;
  }

  :global(.card) {
    background: #181825;
    border: 1px solid #313244;
    border-radius: 8px;
    padding: 20px;
  }

  :global(.btn) {
    padding: 8px 16px;
    border: none;
    border-radius: 6px;
    cursor: pointer;
    font-size: 13px;
    font-weight: 500;
    transition: all 0.2s;
  }

  :global(.btn-primary) {
    background: #89b4fa;
    color: #1e1e2e;
  }

  :global(.btn-primary:hover) {
    background: #74c7ec;
  }

  :global(.btn-primary:disabled) {
    background: #45475a;
    color: #6c7086;
    cursor: not-allowed;
  }

  :global(.btn-secondary) {
    background: #313244;
    color: #cdd6f4;
  }

  :global(.btn-secondary:hover) {
    background: #45475a;
  }

  :global(.btn-danger) {
    background: #f38ba8;
    color: #1e1e2e;
  }

  :global(.btn-danger:hover) {
    background: #eba0ac;
  }

  :global(.btn-danger:disabled) {
    background: #45475a;
    color: #6c7086;
    cursor: not-allowed;
  }

  :global(.btn-sm) {
    padding: 4px 10px;
    font-size: 12px;
  }

  :global(h2) {
    font-size: 22px;
    font-weight: 600;
    margin-bottom: 16px;
  }

  :global(h3) {
    font-size: 16px;
    font-weight: 600;
    margin-bottom: 8px;
  }

  :global(.input) {
    padding: 8px 12px;
    background: #313244;
    border: 1px solid #45475a;
    border-radius: 6px;
    color: #cdd6f4;
    font-size: 13px;
    outline: none;
  }

  :global(.input:focus) {
    border-color: #89b4fa;
  }

  :global(.badge-ok) {
    color: #a6e3a1;
    font-weight: 600;
  }

  :global(.badge-warn) {
    color: #f9e2af;
    font-weight: 600;
  }

  :global(.badge-fail) {
    color: #f38ba8;
    font-weight: 600;
  }

  :global(.separator) {
    border: none;
    border-top: 1px solid #313244;
    margin: 16px 0;
  }
</style>
