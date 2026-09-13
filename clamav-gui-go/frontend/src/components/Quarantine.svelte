<script>
  import { onMount } from 'svelte'
  import { LoadQuarantine, RemoveQuarantineItem, RestoreQuarantineItem } from '../../wailsjs/go/main/App'

  let items = []

  onMount(async () => {
    await refresh()
  })

  async function refresh() {
    try {
      items = await LoadQuarantine() || []
    } catch (e) {
      console.error(e)
    }
  }

  async function removeItem(item) {
    if (!confirm('Are you sure you want to permanently delete this quarantined file?')) return
    try {
      await RemoveQuarantineItem(item.id, item.quarantinePath)
      await refresh()
    } catch (e) {
      alert('Failed to delete: ' + e)
    }
  }

  async function restoreItem(item) {
    try {
      const result = await RestoreQuarantineItem(item.id, item.quarantinePath, item.originalPath)
      if (result) alert(result)
      await refresh()
    } catch (e) {
      alert('Failed to restore: ' + e)
    }
  }

  function formatDate(ts) {
    if (!ts) return ''
    const d = new Date(ts)
    return d.toLocaleString()
  }
</script>

<h2>Quarantine</h2>

<div class="btn-row" style="margin-bottom:12px">
  <button class="btn btn-secondary" on:click={refresh}>Refresh</button>
</div>

<div class="table-wrapper">
  <table>
    <thead>
      <tr>
        <th>Quarantined At</th>
        <th>Threat</th>
        <th>Original Path</th>
        <th>Quarantine File</th>
        <th>Actions</th>
      </tr>
    </thead>
    <tbody>
      {#each items as item}
        <tr>
          <td class="nowrap">{formatDate(item.quarantinedAt)}</td>
          <td class="badge-fail">{item.threatName}</td>
          <td class="path-cell" title={item.originalPath}>{item.originalPath}</td>
          <td class="path-cell" title={item.quarantinePath}>{item.quarantinePath}</td>
          <td class="nowrap">
            <button class="btn btn-sm btn-secondary" on:click={() => restoreItem(item)}>Restore</button>
            <button class="btn btn-sm btn-danger" on:click={() => removeItem(item)}>Delete</button>
          </td>
        </tr>
      {/each}
      {#if items.length === 0}
        <tr><td colspan="5" style="text-align:center; opacity:0.6; padding:24px">No quarantined files.</td></tr>
      {/if}
    </tbody>
  </table>
</div>

<style>
  .btn-row {
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

  .path-cell {
    max-width: 200px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
</style>
