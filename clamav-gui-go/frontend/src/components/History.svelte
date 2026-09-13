<script>
  import { onMount } from 'svelte'
  import { LoadHistory, DeleteHistoryEvent, ClearHistory, ExportHistory } from '../../wailsjs/go/main/App'

  let events = []
  let filterText = ''
  let filterType = 'All'
  const filterTypes = ['All', 'Scan', 'Update', 'Update Failed', 'Config Initialized']

  onMount(async () => {
    await refresh()
  })

  async function refresh() {
    try {
      events = await LoadHistory() || []
    } catch (e) {
      console.error(e)
    }
  }

  async function deleteEvent(id) {
    if (!confirm('Are you sure you want to delete this history entry?')) return
    try {
      await DeleteHistoryEvent(id)
      await refresh()
    } catch (e) {
      alert('Failed to delete: ' + e)
    }
  }

  async function clearAll() {
    if (!confirm('Are you sure you want to delete all history entries? This cannot be undone.')) return
    try {
      await ClearHistory()
      events = []
    } catch (e) {
      alert('Failed to clear: ' + e)
    }
  }

  async function exportHistory(format) {
    try {
      const result = await ExportHistory(format)
      if (result) alert(result)
    } catch (e) {
      alert('Export failed: ' + e)
    }
  }

  function viewEvent(event) {
    alert(`Timestamp: ${formatDate(event.timestamp)}\nEvent: ${event.eventType}\n\nDetails:\n${event.details}`)
  }

  function formatDate(ts) {
    if (!ts) return ''
    const d = new Date(ts)
    return d.toLocaleString()
  }

  $: filteredEvents = events.filter(e => {
    const typeMatch = filterType === 'All' || e.eventType === filterType
    const textMatch = !filterText ||
      (e.details && e.details.toLowerCase().includes(filterText.toLowerCase())) ||
      (e.eventType && e.eventType.toLowerCase().includes(filterText.toLowerCase()))
    return typeMatch && textMatch
  })
</script>

<h2>History</h2>

<div class="toolbar">
  <div class="btn-row">
    <button class="btn btn-secondary" on:click={refresh}>Refresh History</button>
    <button class="btn btn-secondary" on:click={() => exportHistory('csv')}>Export CSV</button>
    <button class="btn btn-secondary" on:click={() => exportHistory('json')}>Export JSON</button>
    <button class="btn btn-danger" on:click={clearAll} disabled={events.length === 0}>Clear All History</button>
  </div>
  <div class="filters">
    <select class="input" bind:value={filterType}>
      {#each filterTypes as type}
        <option value={type}>{type}</option>
      {/each}
    </select>
    <input class="input" type="text" bind:value={filterText} placeholder="Search history..." />
  </div>
</div>

<div class="table-wrapper">
  <table>
    <thead>
      <tr>
        <th>Timestamp</th>
        <th>Event</th>
        <th>Details</th>
        <th>Actions</th>
      </tr>
    </thead>
    <tbody>
      {#each filteredEvents as event}
        <tr>
          <td class="nowrap">{formatDate(event.timestamp)}</td>
          <td>{event.eventType}</td>
          <td class="details-cell" title={event.details}>{event.details}</td>
          <td class="nowrap">
            <button class="btn btn-sm btn-secondary" on:click={() => viewEvent(event)}>View</button>
            <button class="btn btn-sm btn-danger" on:click={() => deleteEvent(event.id)}>Delete</button>
          </td>
        </tr>
      {/each}
      {#if filteredEvents.length === 0}
        <tr><td colspan="4" style="text-align:center; opacity:0.6; padding:24px">No history entries.</td></tr>
      {/if}
    </tbody>
  </table>
</div>

<style>
  .toolbar {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 12px;
    flex-wrap: wrap;
    gap: 8px;
  }

  .btn-row {
    display: flex;
    gap: 8px;
  }

  .filters {
    display: flex;
    gap: 8px;
  }

  .table-wrapper {
    border: 1px solid #313244;
    border-radius: 6px;
    overflow: auto;
    max-height: 500px;
  }

  table {
    width: 100%;
    border-collapse: collapse;
    font-size: 13px;
  }

  th {
    position: sticky;
    top: 0;
    background: #181825;
    padding: 10px 12px;
    text-align: left;
    border-bottom: 1px solid #313244;
    font-weight: 600;
  }

  td {
    padding: 8px 12px;
    border-bottom: 1px solid #313244;
  }

  .nowrap {
    white-space: nowrap;
  }

  .details-cell {
    max-width: 300px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
</style>
