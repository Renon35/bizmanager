// ── Unified Excel Import UI ────────────────────────────────────────────────

let _importFile = null;
let _previewItems = [];

async function renderImport() {
  const [dealers, brands] = await Promise.all([
    api('/dealers').catch(() => []),
    api('/brands').catch(() => [])
  ]);

  const dealerOptions = dealers.map(d => `<option value="${d.id}">${d.name}</option>`).join('');
  const brandOptions = brands.map(b => `<option value="${b.id}" data-structure="${b.codeStructure}">${b.name}</option>`).join('');

  document.getElementById('page-content').innerHTML = `
  <div class="imp-page">
    <div class="imp-page-header">
      <div class="imp-page-header-icon">
        <svg viewBox="0 0 24 24" width="28" height="28" stroke="currentColor" fill="none" stroke-width="1.8">
          <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z"/>
          <polyline points="14,2 14,8 20,8"/>
          <line x1="12" y1="18" x2="12" y2="12"/>
          <polyline points="9,15 12,12 15,15"/>
        </svg>
      </div>
      <div>
        <h1 class="imp-page-title">Toplu Veri İçe Aktarma</h1>
        <p class="imp-page-sub">Tek bir Excel dosyası ile ürünleri, fiyatları ve stokları içeri aktarın.</p>
      </div>
    </div>

    <div class="imp-pane-card card">
      <div class="imp-pane-card-header">
        <div class="imp-pane-icon imp-icon-blue">
          <svg viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" fill="none" stroke-width="2"><path d="M21 16V8a2 2 0 00-1-1.73l-7-4a2 2 0 00-2 0l-7 4A2 2 0 003 8v8a2 2 0 001 1.73l7 4a2 2 0 002 0l7-4A2 2 0 0021 16z"/></svg>
        </div>
        <div>
          <h2>Excel Yükle</h2>
          <p>Lütfen ilgili seçimleri yapın ve dosyanızı yükleyin.</p>
        </div>
      </div>

      <div class="imp-pane-body">
        <div class="imp-info-note">
          <svg viewBox="0 0 24 24" width="15" height="15" stroke="currentColor" fill="none" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>
          Sistem ürün kodu ile eşleştirme yapar. Ürün yoksa oluşturulur, fiyat ve stok bilgileri seçili bayi için güncellenir.
        </div>

        <div class="imp-col-chips" id="imp-required-columns">
          <span class="imp-chip">Ürün Kodu</span>
          <span class="imp-chip">Ürün Adı</span>
          <span class="imp-chip">Alış Fiyatı</span>
          <span class="imp-chip">Satış Fiyatı</span>
          <span class="imp-chip">Liste Fiyatı</span>
          <span class="imp-chip">Koli Fiyatı</span>
          <span class="imp-chip">Paket Fiyatı</span>
          <span class="imp-chip">Birim Fiyatı</span>
          <span class="imp-chip">Stok</span>
        </div>

        <!-- Selections -->
        <div class="form-row">
          <div class="form-group">
            <label>Marka *</label>
            <select class="form-control" id="import-brand" onchange="onBrandChange()" required>
              <option value="">— Marka Seçin —</option>
              ${brandOptions}
            </select>
          </div>
          <div class="form-group">
            <label>Bayi *</label>
            <select class="form-control" id="import-dealer" required>
              <option value="">— Bayi Seçin —</option>
              ${dealerOptions}
            </select>
          </div>
          <div class="form-group">
            <label>Fiyat Türü *</label>
            <select class="form-control" id="import-price-type" required>
              <option value="purchase_price">Alış Fiyatı</option>
              <option value="sale_price">Satış Fiyatı</option>
              <option value="list_price">Liste Fiyatı</option>
            </select>
          </div>
        </div>

        <!-- Upload zone -->
        ${buildUploadZone('.xlsx, .xls')}

        <!-- Preview -->
        <div id="preview-unified" class="imp-preview hidden">
          <div class="imp-preview-header">
            <span id="preview-unified-label" class="imp-preview-filename"></span>
            <button class="btn btn-ghost btn-sm" onclick="clearImportFile()">✕ Temizle</button>
          </div>
          <div class="imp-table-wrap">
            <table id="preview-unified-table">
              <thead><tr id="preview-unified-header">
                <th>Tür</th><th>Ürün Kodu</th><th>Ürün Adı</th><th>Koleksiyon</th><th>Fiyat</th><th>Stok</th><th>Durum</th><th>İşlemler</th>
              </tr></thead>
              <tbody id="preview-unified-body"></tbody>
            </table>
          </div>
        </div>

        <div class="imp-actions" id="imp-action-buttons">
          <button class="btn btn-primary" id="btn-import-preview" onclick="doPreview()" style="display:none;">
            <svg viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" fill="none" stroke-width="2"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>
            Önizleme Oluştur
          </button>
          
          <button class="btn btn-success" id="btn-import-commit" onclick="doCommit()" style="display:none;">
            <svg viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" fill="none" stroke-width="2"><path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4"/><polyline points="17,8 12,3 7,8"/><line x1="12" y1="3" x2="12" y2="15"/></svg>
            Verileri İçe Aktar (Onayla)
          </button>
        </div>

        <div id="result-unified" class="imp-result hidden"></div>
      </div>
    </div>

    <!-- BULK IMAGE UPLOAD SECTION -->
    <div class="imp-pane-card card" style="margin-top:24px;">
      <div class="imp-pane-card-header">
        <div class="imp-pane-icon" style="background:#f0abfc;color:#a21caf">
          <svg viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" fill="none" stroke-width="2"><rect x="3" y="3" width="18" height="18" rx="2" ry="2"/><circle cx="8.5" cy="8.5" r="1.5"/><polyline points="21 15 16 10 5 21"/></svg>
        </div>
        <div>
          <h2>Toplu Görsel Yükle</h2>
          <p>Ürün görsellerini dosya ismine göre (Ürün Kodu veya Barkod) otomatik eşleştirin.</p>
        </div>
      </div>
      <div class="imp-pane-body">
        <div class="imp-info-note" style="background:#fdf4ff;color:#86198f;border-color:#fae8ff">
          <svg viewBox="0 0 24 24" width="15" height="15" stroke="currentColor" fill="none" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>
          Görsellerin isimleri tam olarak <strong>Ürün Kodu</strong> veya <strong>Barkod</strong> ile eşleşmelidir. (Örnek: <code style="background:#f5d0fe;color:#701a75">CC330.jpg</code>)
        </div>
        
        <div class="imp-upload-zone" id="zone-images" onclick="triggerImagePick()">
          <svg viewBox="0 0 24 24" width="36" height="36" stroke="currentColor" fill="none" stroke-width="1.5">
            <rect x="3" y="3" width="18" height="18" rx="2" ry="2"></rect>
            <circle cx="8.5" cy="8.5" r="1.5"></circle>
            <polyline points="21 15 16 10 5 21"></polyline>
          </svg>
          <p class="imp-upload-title">Görselleri seçmek için tıklayın</p>
          <p class="imp-upload-sub">Çoklu seçim yapabilirsiniz &nbsp;·&nbsp; <strong>.jpg, .png, .webp</strong></p>
          <input type="file" id="file-input-images" accept=".jpg, .jpeg, .png, .webp" multiple style="display:none" onchange="handleImageSelect(event)">
        </div>

        <div class="imp-actions">
          <button class="btn btn-primary" id="btn-upload-images" onclick="doBulkImageUpload()" style="background:#a21caf;border-color:#a21caf" disabled>
            <svg viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" fill="none" stroke-width="2"><path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4"/><polyline points="17,8 12,3 7,8"/><line x1="12" y1="3" x2="12" y2="15"/></svg>
            Görselleri Yükle
          </button>
        </div>
        <div id="result-images" class="imp-result hidden"></div>
      </div>
    </div>
  </div>`;

  attachImportEvents();
  attachImageEvents();
}

async function onBrandChange() {
  const brandId = document.getElementById('import-brand').value;

  if (!brandId) return;

  // Update required columns UI based on CodeStructure
  const brandSelect = document.getElementById('import-brand');
  const struct = brandSelect.options[brandSelect.selectedIndex]?.dataset.structure || '';
  const chipsEl = document.getElementById('imp-required-columns');

  if (chipsEl) {
    let cols = [];
    if (struct === 'barcode') {
      cols = ['Barkod', 'Ürün Adı', 'Fiyat', 'Stok'];
    } else if (struct === 'dual_code') {
      cols = ['Kalıp Kodu', 'Ürün Kodu', 'Ürün Adı', 'Fiyat', 'Stok'];
    } else {
      cols = ['Ürün Kodu', 'Ürün Adı', 'Fiyat', 'Stok'];
    }

    chipsEl.innerHTML = cols.map(c => `<span class="imp-chip">${c}</span>`).join('');
  }
}

function buildUploadZone(accept) {
  return `
  <div class="imp-upload-zone" id="zone-unified" onclick="triggerFilePick()">
    <svg viewBox="0 0 24 24" width="36" height="36" stroke="currentColor" fill="none" stroke-width="1.5">
      <path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4"/>
      <polyline points="17,8 12,3 7,8"/>
      <line x1="12" y1="3" x2="12" y2="15"/>
    </svg>
    <p class="imp-upload-title">Dosya yüklemek için tıklayın</p>
    <p class="imp-upload-sub">veya sürükleyip bırakın &nbsp;·&nbsp; <strong>${accept}</strong></p>
    <input type="file" id="file-input-unified" accept="${accept}" style="display:none" onchange="handleFileSelect(event)">
  </div>`;
}

function triggerFilePick() {
  document.getElementById('file-input-unified')?.click();
}

function attachImportEvents() {
  const zone = document.getElementById('zone-unified');
  if (!zone) return;
  zone.addEventListener('dragover', e => { e.preventDefault(); zone.classList.add('drag-over'); });
  zone.addEventListener('dragleave', () => zone.classList.remove('drag-over'));
  zone.addEventListener('drop', e => {
    e.preventDefault();
    zone.classList.remove('drag-over');
    const file = e.dataTransfer?.files?.[0];
    if (file) processFile(file);
  });
}

function handleFileSelect(event) {
  const file = event.target.files?.[0];
  if (file) processFile(file);
}

function processFile(file) {
  _importFile = file;

  const zone = document.getElementById('zone-unified');
  if (zone) {
    zone.classList.add('has-file');
    zone.querySelector('.imp-upload-title').textContent = file.name;
    zone.querySelector('.imp-upload-sub').textContent = `${(file.size / 1024).toFixed(1)} KB`;
  }

  const previewEl = document.getElementById('preview-unified');
  const labelEl = document.getElementById('preview-unified-label');
  const previewBtn = document.getElementById('btn-import-preview');
  const commitBtn = document.getElementById('btn-import-commit');

  if (previewEl) previewEl.classList.remove('hidden');
  if (labelEl) labelEl.textContent = `📎 ${file.name}`;
  if (previewBtn) previewBtn.style.display = 'inline-flex';
  if (commitBtn) commitBtn.style.display = 'none';

  // We no longer build CSV preview directly. 
  // Instruct user to click Preview.
  const tbody = document.getElementById('preview-unified-body');
  if (tbody) {
    tbody.innerHTML = `<tr><td colspan="6" class="imp-preview-placeholder">
      <svg viewBox="0 0 24 24" width="18" height="18" stroke="#10b981" fill="none" stroke-width="2" style="vertical-align:middle;margin-right:6px">
        <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z"/>
        <polyline points="14,2 14,8 20,8"/>
      </svg>
      Dosya seçildi. Verileri kontrol etmek için "Önizleme Oluştur" butonuna tıklayın.
    </td></tr>`;
  }
}

function clearImportFile() {
  _importFile = null;
  _previewItems = [];
  const zone = document.getElementById('zone-unified');
  if (zone) {
    zone.classList.remove('has-file');
    zone.querySelector('.imp-upload-title').textContent = 'Dosya yüklemek için tıklayın';
    zone.querySelector('.imp-upload-sub').innerHTML = 'veya sürükleyip bırakın';
  }
  const fileInput = document.getElementById('file-input-unified');
  if (fileInput) fileInput.value = '';
  document.getElementById('preview-unified')?.classList.add('hidden');
  const resultEl = document.getElementById('result-unified');
  if (resultEl) { resultEl.classList.add('hidden'); resultEl.innerHTML = ''; }

  const previewBtn = document.getElementById('btn-import-preview');
  const commitBtn = document.getElementById('btn-import-commit');
  if (previewBtn) previewBtn.style.display = 'none';
  if (commitBtn) commitBtn.style.display = 'none';
}

async function doPreview() {
  if (!_importFile) { toast('Lütfen önce bir dosya seçin.', 'danger'); return; }

  const brandId = document.getElementById('import-brand').value;
  const dealerId = document.getElementById('import-dealer').value;

  if (!brandId || !dealerId) {
    toast('Lütfen Marka ve Bayi seçimlerini tamamlayın.', 'danger');
    return;
  }

  const btn = document.getElementById('btn-import-preview');
  const commitBtn = document.getElementById('btn-import-commit');
  const resultEl = document.getElementById('result-unified');
  const tbody = document.getElementById('preview-unified-body');

  const fd = new FormData();
  fd.append('file', _importFile);

  const priceType = document.getElementById('import-price-type').value;

  if (btn) { btn.disabled = true; btn.innerHTML = 'Önizleme Hazırlanıyor…'; }
  if (resultEl) resultEl.classList.add('hidden');

  try {
    const res = await fetch(`/api/import/preview?brandId=${brandId}&dealerId=${dealerId}&priceType=${priceType}`, {
      method: 'POST', body: fd
    });
    const data = await res.json();
    if (!res.ok) throw new Error(data.error || 'Bilinmeyen hata');

    _previewItems = data.items || [];

    if (_previewItems.length === 0) {
      if (tbody) tbody.innerHTML = `<tr><td colspan="6" style="text-align:center;padding:15px;color:#ef4444">Dosyada okunabilir ürün veya koleksiyon bulunamadı.</td></tr>`;
      return;
    }

    if (tbody) {
      const displayRows = _previewItems.slice(0, 100);
      tbody.innerHTML = displayRows.map(item => {
        if (item.isHeader) {
          return `<tr style="background:#f1f5f9;font-weight:600"><td colspan="8" style="color:#0f172a">📁 Koleksiyon: ${item.collection}</td></tr>`;
        }

        let codeStr = item.productCode || '—';
        if (item.moldCode && item.productCode) codeStr = `${item.moldCode} / ${item.productCode}`;
        if (item.barcode) codeStr = item.barcode;

        let statusClass = 'badge-success';
        let rowClass = '';
        if (item.status.startsWith('Error:')) { statusClass = 'badge-danger'; rowClass = 'style="background-color:#fee2e2"'; }
        else if (item.status.startsWith('Warning:')) { statusClass = 'badge-warning'; rowClass = 'style="background-color:#fef3c7"'; }

        return `<tr ${rowClass}>
          <td><span class="badge ${item.isHeader ? 'badge-primary' : 'badge-light'}">Ürün</span></td>
          <td>${codeStr}</td>
          <td>${item.productName || '—'}</td>
          <td><span style="color:#64748b;font-size:12px">${item.collection || '—'}</span></td>
          <td><strong>${item.price} ₺</strong></td>
          <td>${item.stock} ad.</td>
          <td><span class="badge ${statusClass}">${item.status}</span></td>
          <td>
             <button class="btn btn-sm btn-ghost" onclick="editPreviewRow(${item.id})">✏️</button>
             <button class="btn btn-sm btn-ghost" onclick="deletePreviewRow(${item.id})">❌</button>
          </td>
        </tr>`;
      }).join('');

      if (_previewItems.length > 100) {
        tbody.innerHTML += `<tr><td colspan="8" style="text-align:center;color:#94a3b8;font-size:12px;padding:10px">… ve ${_previewItems.length - 100} satır daha</td></tr>`;
      }
    }

    if (btn) btn.style.display = 'none';
    if (commitBtn) commitBtn.style.display = 'inline-flex';
    toast('Önizleme başarıyla oluşturuldu. Satırları kontrol edip onaylayın.', 'info');

  } catch (err) {
    if (tbody) tbody.innerHTML = `<tr><td colspan="6" style="text-align:center;color:#ef4444;padding:15px">Hata: ${err.message}</td></tr>`;
    toast('Önizleme başarısız: ' + err.message, 'danger');
  } finally {
    if (btn) {
      btn.disabled = false;
      btn.innerHTML = `<svg viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" fill="none" stroke-width="2"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg> Önizleme Oluştur`;
    }
  }
}

async function doCommit() {
  if (_previewItems.length === 0) { toast('Aktarılacak veri bulunamadı.', 'danger'); return; }

  const brandId = document.getElementById('import-brand').value;
  const dealerId = document.getElementById('import-dealer').value;
  const priceType = document.getElementById('import-price-type').value;

  const btn = document.getElementById('btn-import-commit');
  const resultEl = document.getElementById('result-unified');

  if (btn) { btn.disabled = true; btn.textContent = 'Veriler Aktarılıyor…'; }
  if (resultEl) resultEl.classList.add('hidden');

  const payload = {
    brandId: parseInt(brandId, 10),
    dealerId: parseInt(dealerId, 10),
    priceType: priceType,
    items: _previewItems
  };

  try {
    const res = await fetch(`/api/import/commit`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });
    const data = await res.json();

    if (!res.ok) throw new Error(data.error || 'Bilinmeyen hata');

    if (resultEl) {
      resultEl.classList.remove('hidden');
      resultEl.innerHTML = buildResultHTML(data);
    }
    toast('İçe aktarma başarıyla tamamlandı.', 'success');

  } catch (err) {
    if (resultEl) {
      resultEl.classList.remove('hidden');
      resultEl.innerHTML = `<div class="imp-result-error">
        <svg viewBox="0 0 24 24" width="18" height="18" stroke="currentColor" fill="none" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>
        Hata: ${err.message}
      </div>`;
    }
    toast('İçe aktarma başarısız: ' + err.message, 'danger');
  } finally {
    if (btn) {
      btn.disabled = false;
      btn.innerHTML = `<svg viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" fill="none" stroke-width="2"><path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4"/><polyline points="17,8 12,3 7,8"/><line x1="12" y1="3" x2="12" y2="15"/></svg> İçe Aktar (Onayla)`;
    }
  }
}

async function deletePreviewRow(id) {
  if (!confirm('Bu satırı silmek istediğinize emin misiniz?')) return;
  try {
    const res = await fetch(`/api/import/preview/${id}`, { method: 'DELETE' });
    if (!res.ok) throw new Error('Silinemedi');
    _previewItems = _previewItems.filter(item => item.id !== id);
    // Re-render
    const btn = document.getElementById('btn-import-preview');
    doPreviewRenderOnly();
    toast('Satır silindi', 'success');
  } catch (e) { toast(e.message, 'danger'); }
}

async function editPreviewRow(id) {
  const item = _previewItems.find(i => i.id === id);
  if (!item) return;

  const newName = prompt('Yeni Ürün Adı:', item.productName || '');
  if (newName === null) return;
  const newCode = prompt('Yeni Ürün Kodu:', item.productCode || '');
  if (newCode === null) return;
  const newPrice = prompt('Yeni Fiyat:', item.price || 0);
  if (newPrice === null) return;

  item.productName = newName;
  item.productCode = newCode;
  item.price = parseFloat(newPrice) || 0;

  try {
    const res = await fetch(`/api/import/preview/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(item)
    });
    if (!res.ok) throw new Error('Güncellenemedi');
    const updated = await res.json();

    // Update local array
    const idx = _previewItems.findIndex(i => i.id === id);
    if (idx !== -1) _previewItems[idx] = updated;

    doPreviewRenderOnly();
    toast('Satır güncellendi', 'success');
  } catch (e) { toast(e.message, 'danger'); }
}

function doPreviewRenderOnly() {
  const tbody = document.getElementById('preview-unified-body');
  if (!tbody) return;
  if (_previewItems.length === 0) {
    tbody.innerHTML = `<tr><td colspan="8" style="text-align:center;padding:15px;color:#ef4444">Tüm satırlar silindi.</td></tr>`;
    return;
  }
  const displayRows = _previewItems.slice(0, 100);
  tbody.innerHTML = displayRows.map(item => {
    if (item.isHeader) {
      return `<tr style="background:#f1f5f9;font-weight:600"><td colspan="8" style="color:#0f172a">📁 Koleksiyon: ${item.collection}</td></tr>`;
    }

    let codeStr = item.productCode || '—';
    if (item.moldCode && item.productCode) codeStr = `${item.moldCode} / ${item.productCode}`;
    if (item.barcode) codeStr = item.barcode;

    let statusClass = 'badge-success';
    let rowClass = '';
    if (item.status.startsWith('Error:')) { statusClass = 'badge-danger'; rowClass = 'style="background-color:#fee2e2"'; }
    else if (item.status.startsWith('Warning:')) { statusClass = 'badge-warning'; rowClass = 'style="background-color:#fef3c7"'; }

    return `<tr ${rowClass}>
      <td><span class="badge ${item.isHeader ? 'badge-primary' : 'badge-light'}">Ürün</span></td>
      <td>${codeStr}</td>
      <td>${item.productName || '—'}</td>
      <td><span style="color:#64748b;font-size:12px">${item.collection || '—'}</span></td>
      <td><strong>${item.price} ₺</strong></td>
      <td>${item.stock} ad.</td>
      <td><span class="badge ${statusClass}">${item.status}</span></td>
      <td>
         <button class="btn btn-sm btn-ghost" onclick="editPreviewRow(${item.id})">✏️</button>
         <button class="btn btn-sm btn-ghost" onclick="deletePreviewRow(${item.id})">❌</button>
      </td>
    </tr>`;
  }).join('');

  if (_previewItems.length > 100) {
    tbody.innerHTML += `<tr><td colspan="8" style="text-align:center;color:#94a3b8;font-size:12px;padding:10px">… ve ${_previewItems.length - 100} satır daha</td></tr>`;
  }
}

function buildResultHTML(data) {
  const cards = [
    { label: 'Oluşturulan Ürün', value: data.productsCreated ?? 0, color: 'primary' },
    { label: 'Güncellenen Ürün', value: data.productsUpdated ?? 0, color: 'success' },
    { label: 'Fiyat & Stok İşlemi', value: data.dealerPricesUpdated ?? 0, color: 'info' },
    { label: 'Atlanan Satır', value: data.rowsSkipped ?? 0, color: 'muted' },
  ];

  const badge = `<div class="imp-result-success-badge">
    <svg viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" fill="none" stroke-width="2.5"><polyline points="20,6 9,17 4,12"/></svg>
    İçe aktarma tamamlandı
  </div>`;

  const cardsHTML = cards.map(c => `
    <div class="imp-result-card imp-result-${c.color}">
      <div class="imp-result-value">${c.value}</div>
      <div class="imp-result-label">${c.label}</div>
    </div>`).join('');

  return `${badge}<div class="imp-result-grid">${cardsHTML}</div>`;
}

// ── Bulk Image Upload Handling ──────────────────────────────────────────────
let _pendingImages = [];

function attachImageEvents() {
  const dropZone = document.getElementById('image-drop-zone');
  const fileInput = document.getElementById('import-images');
  const btnSelect = document.getElementById('btn-select-images');
  const btnUpload = document.getElementById('btn-upload-images');

  if (!dropZone || !fileInput) return;

  btnSelect.addEventListener('click', () => fileInput.click());

  fileInput.addEventListener('change', (e) => {
    handleImageFiles(e.target.files);
  });

  dropZone.addEventListener('dragover', (e) => {
    e.preventDefault();
    dropZone.classList.add('drag-active');
  });

  dropZone.addEventListener('dragleave', (e) => {
    e.preventDefault();
    dropZone.classList.remove('drag-active');
  });

  dropZone.addEventListener('drop', (e) => {
    e.preventDefault();
    dropZone.classList.remove('drag-active');
    handleImageFiles(e.dataTransfer.files);
  });
}

function handleImageFiles(files) {
  if (!files || files.length === 0) return;

  for (let i = 0; i < files.length; i++) {
    const f = files[i];
    if (f.type.startsWith('image/')) {
      _pendingImages.push(f);
    }
  }

  document.getElementById('image-queue-count').textContent = _pendingImages.length;
  document.getElementById('btn-upload-images').disabled = (_pendingImages.length === 0);

  toast(`${files.length} görsel sıraya eklendi.`, 'success');
}

async function doBulkImageUpload() {
  if (_pendingImages.length === 0) return;

  const btn = document.getElementById('btn-upload-images');
  const resultEl = document.getElementById('result-images');

  btn.disabled = true;
  btn.innerHTML = `<svg viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" fill="none" stroke-width="2"><circle cx="12" cy="12" r="10"/><path d="M12 8v4l3 3"/></svg> Yükleniyor...`;

  const formData = new FormData();
  _pendingImages.forEach(f => formData.append('files', f));

  try {
    const res = await fetch('/api/products/bulk-images', {
      method: 'POST',
      body: formData
    });
    const data = await res.json();

    if (resultEl) {
      resultEl.classList.remove('hidden');
      resultEl.innerHTML = `
        <div class="imp-result-success-badge" style="background:var(--success-lt);color:var(--success-dk)">
          <svg viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" fill="none" stroke-width="2"><polyline points="20,6 9,17 4,12"/></svg>
          Yükleme Tamamlandı: ${data.matchedCount} görsel eşleştirildi. ${data.unmatchedCount} görsel eşleştirilemedi.
        </div>`;
    }

    _pendingImages = [];
    document.getElementById('image-queue-count').textContent = '0';
    toast('Görsel eşleşmeleri başarıyla işlendi.', 'success');
  } catch (err) {
    toast('Görseller yüklenirken hata oluştu.', 'danger');
  } finally {
    btn.disabled = false;
    btn.innerHTML = `
      <svg viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" fill="none" stroke-width="2"><path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4"/><polyline points="17,8 12,3 7,8"/><line x1="12" y1="3" x2="12" y2="15"/></svg>
      Görselleri Yükle
    `;
  }
}
