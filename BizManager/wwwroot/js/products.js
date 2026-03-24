// Ürünler — Katalog ve Bayi destekli listeleme

async function renderProducts() {
  const brands = await api('/brands').catch(() => []);
  window.selectedProductIds = new Set(); // Reset selection on render

  document.getElementById('page-content').innerHTML = `
  <div class="card">
    <div class="card-header" style="flex-wrap:wrap;gap:12px">
      <h2>Ürünler</h2>
      <div style="display:flex;gap:8px">
        <button class="btn btn-outline btn-sm" onclick="exportProductListPdf()">
          <svg viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" fill="none" stroke-width="2" style="margin-right:4px;vertical-align:text-bottom"><path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/><polyline points="10 9 9 9 8 9"/></svg>
          PDF İndir
        </button>
        <button class="btn btn-primary btn-sm" onclick="openProductModal()">+ Ürün Ekle</button>
      </div>
    </div>
    <div class="card-body" style="border-bottom:1px solid #e2e8f0;background:#f8fafc;padding:12px 20px">
      <div class="form-row" style="margin:0;align-items:end">
        <div class="form-group" style="margin:0;flex:1">
          <label>Arama</label>
          <input type="text" class="form-control" id="filter-q" placeholder="Ürün adı, kod, barkod..." onkeyup="if(event.key==='Enter') loadProducts()">
        </div>
        <div class="form-group" style="margin:0;flex:1">
          <label>Marka</label>
          <select class="form-control" id="filter-brand" onchange="onFilterBrandChange()">
            <option value="">Tümü</option>
            ${brands.map(b => `<option value="${b.id}">${b.name}</option>`).join('')}
          </select>
        </div>
        <div class="form-group" style="margin:0;flex:1">
          <label>Katalog</label>
          <select class="form-control" id="filter-catalog" onchange="onFilterCatalogChange()" disabled>
            <option value="">Tümü</option>
          </select>
        </div>
        <div class="form-group" style="margin:0;flex:1">
          <label>Koleksiyon</label>
          <select class="form-control" id="filter-collection" disabled>
            <option value="">Tümü</option>
          </select>
        </div>
        <button class="btn btn-primary btn-sm" style="height:38px" onclick="loadProducts()">Filtrele</button>
      </div>
    </div>
    
    <!-- Bulk Selection Action Bar -->
    <div id="bulk-action-bar" style="display:none;background:#ebf8ff;border-bottom:1px solid #bae6fd;padding:12px 20px;align-items:center;justify-content:space-between">
      <div style="font-weight:600;color:#0369a1" id="bulk-selection-count">0 ürün seçildi</div>
      <div style="display:flex;gap:8px;align-items:center">
        <select class="form-control" id="bulk-action-select" style="width:200px;margin:0" onchange="executeBulkAction()">
          <option value="">— Toplu İşlem —</option>
          <option value="change-collection">Koleksiyon Değiştir</option>
          <option value="update-price">Fiyat Güncelle (%)</option>
          <option value="assign-image">Görsel Ata</option>
          <option value="export-pdf">Seçilenleri PDF İndir</option>
          <option value="export-excel">Seçilenleri Excel İndir</option>
          <option value="delete" style="color:red">Seçilenleri Sil</option>
        </select>
        <button class="btn btn-outline btn-sm" onclick="clearProductSelection()">Seçimi Temizle</button>
      </div>
    </div>

    <div class="card-body">
      <div id="products-container" style="display:flex;flex-direction:column;gap:24px;">
        <div style="text-align:center;padding:20px;color:#94a3b8">Yükleniyor…</div>
      </div>
    </div>
  </div>`;
  await loadProducts();
}

async function onFilterBrandChange() {
  const brandId = document.getElementById('filter-brand').value;
  const catSel = document.getElementById('filter-catalog');
  const colSel = document.getElementById('filter-collection');

  catSel.innerHTML = '<option value="">Tümü</option>';
  colSel.innerHTML = '<option value="">Tümü</option>';
  catSel.disabled = true;
  colSel.disabled = true;

  if (brandId) {
    try {
      const catalogs = await api(`/catalogs?brandId=${brandId}`);
      if (catalogs.length) {
        catSel.innerHTML += catalogs.map(c => `<option value="${c.id}">${c.catalogName}</option>`).join('');
        catSel.disabled = false;
      }
    } catch (e) { }
  }
}

async function onFilterCatalogChange() {
  const catalogId = document.getElementById('filter-catalog').value;
  const colSel = document.getElementById('filter-collection');

  colSel.innerHTML = '<option value="">Tümü</option>';
  colSel.disabled = true;

  if (catalogId) {
    try {
      const cols = await api(`/collections?catalogId=${catalogId}`);
      if (cols.length) {
        colSel.innerHTML += cols.map(c => `<option value="${c.id}">${c.collectionName}</option>`).join('');
        colSel.disabled = false;
      }
    } catch (e) { }
  }
}

function getProductFiltersQuery() {
  const q = document.getElementById('filter-q')?.value.trim() || '';
  const bid = document.getElementById('filter-brand')?.value || '';
  const cid = document.getElementById('filter-catalog')?.value || '';
  const colid = document.getElementById('filter-collection')?.value || '';

  const params = new URLSearchParams();
  if (q) params.append('q', q);
  if (bid) params.append('brandId', bid);
  if (cid) params.append('catalogId', cid);
  if (colid) params.append('collectionId', colid);

  return params.toString();
}

async function loadProducts() {
  try {
    const queryStr = getProductFiltersQuery();
    const url = queryStr ? `/products?${queryStr}` : '/products';

    document.getElementById('products-container').innerHTML = '<div style="text-align:center;padding:20px;color:#94a3b8">Yükleniyor…</div>';

    const products = await api(url);
    const container = document.getElementById('products-container');

    if (!products.length) {
      container.innerHTML = '<div class="empty-state"><p>Henüz ürün yok. Başlamak için "+ Ürün Ekle" butonuna tıklayın.</p></div>';
      return;
    }

    // Grouping by Catalog
    const grouped = {};
    products.forEach(p => {
      const catName = p.catalog ? `${p.catalog.brand?.name || 'Bilinmeyen Marka'} - ${p.catalog.catalogName}` : 'Kataloğu Olmayan Ürünler';
      if (!grouped[catName]) grouped[catName] = [];
      grouped[catName].push(p);
    });

    let html = '';
    for (const [catalogName, items] of Object.entries(grouped)) {
      html += `
        <div class="catalog-group">
          <h3 style="margin-bottom:12px;color:#1e293b;border-bottom:2px solid #e2e8f0;padding-bottom:8px;">
            <svg viewBox="0 0 24 24" width="20" height="20" stroke="#3b82f6" fill="none" stroke-width="2" style="vertical-align:text-bottom;margin-right:6px"><path d="M4 19.5v-15A2.5 2.5 0 016.5 2H20v20H6.5a2.5 2.5 0 01-2.5-2.5z"/><path d="M6.5 2H20v20H6.5A2.5 2.5 0 014 19.5v-15A2.5 2.5 0 016.5 2z"/></svg>
            ${catalogName}
          </h3>
          <table class="table" style="margin-bottom:0">
            <thead>
              <tr>
                <th style="width:40px;text-align:center">
                  <input type="checkbox" onchange="toggleCatalogSelection(this, '${catalogName}')" class="master-checkbox-${catalogName.replace(/[^a-zA-Z0-9]/g, '')}">
                </th>
                <th style="width:64px">Görsel</th>
                <th>Ad</th>
                <th>Kodlama</th>
                <th>Alış Fiyatı</th>
                <th>Satış Fiyatı</th>
                <th>Liste Fiyatı</th>
                <th>Koli</th>
                <th>Paket</th>
                <th>Kategori</th>
                <th>Bayiler (${items.reduce((cnt, p) => cnt + (p.dealerProducts?.length || 0), 0)} toplam)</th>
                <th>İşlemler</th>
              </tr>
            </thead>
            <tbody>
              ${items.map(p => {
        const dpCount = p.dealerProducts?.length || 0;
        const imgCell = p.imageUrl
          ? `<img src="${p.imageUrl}" style="width:48px;height:48px;object-fit:cover;border-radius:4px;border:1px solid #e2e8f0;display:block">`
          : p.hasMissingImage
            ? `<div title="Google taramasında görsel bulunamadı" style="width:48px;height:48px;background:#fee2e2;border-radius:4px;border:1px solid #f87171;display:flex;align-items:center;justify-content:center;color:#dc2626;font-size:10px;text-align:center;font-weight:bold;line-height:1.2;cursor:help">Görsel<br>Bulunamadı</div>`
            : `<div style="width:48px;height:48px;background:#f1f5f9;border-radius:4px;border:1px solid #cbd5e1;display:flex;align-items:center;justify-content:center;color:#64748b;font-size:10px;text-align:center;font-weight:bold;line-height:1.2">Görsel<br>Yok</div>`;
        return `
                <tr>
                  <td style="text-align:center">
                    <input type="checkbox" class="product-checkbox pcb-${catalogName.replace(/[^a-zA-Z0-9]/g, '')}" value="${p.id}" onchange="toggleProductSelection(this)" ${window.selectedProductIds?.has(p.id) ? 'checked' : ''}>
                  </td>
                  <td>${imgCell}</td>
                  <td><strong>${p.productName}</strong></td>
                  <td>
                    ${p.barcode ? `<div style="font-size:11px;color:#64748b">Barkod:</div><code style="background:#f1f5f9;padding:2px 4px;border-radius:4px;display:inline-block;margin-bottom:4px">${p.barcode}</code><br>` : ''}
                    ${p.moldCode ? `<div style="font-size:11px;color:#64748b">Kalıp Kodu:</div><code style="background:#f1f5f9;padding:2px 4px;border-radius:4px;display:inline-block;margin-bottom:4px">${p.moldCode}</code><br>` : ''}
                    ${p.productCode ? `<div style="font-size:11px;color:#64748b">Ürün Kodu:</div><code style="background:#f1f5f9;padding:2px 4px;border-radius:4px;display:inline-block">${p.productCode}</code>` : ''}
                    ${!p.barcode && !p.moldCode && !p.productCode ? '—' : ''}
                  </td>
                  <td>${p.purchasePrice} ₺</td>
                  <td>${p.salePrice} ₺</td>
                  <td>${p.listPrice} ₺</td>
                  <td>${p.unitsPerCase != null ? `<span class="badge badge-info">${p.unitsPerCase} adet/koli</span>` : '—'}</td>
                  <td>${p.unitsPerPack != null ? `<span class="badge badge-info">${p.unitsPerPack} adet/paket</span>` : '—'}</td>
                  <td>${p.packageType || '—'}</td>
                  <td>
                    ${dpCount > 0 ?
            `<button class="btn btn-ghost btn-sm" onclick="showProductDealers(${p.id})">Bayileri Gör (${dpCount})</button>` :
            '<span style="color:#94a3b8;font-size:13px">Bayi Yok</span>'
          }
                  </td>
                  <td>
                    <div class="section-actions">
                      <button class="btn btn-ghost btn-sm" onclick='openProductModal(${JSON.stringify(p).replace(/'/g, "&#39;")})'>Düzenle</button>
                      <button class="btn btn-danger btn-sm" onclick="deleteProduct(${p.id})">Sil</button>
                    </div>
                  </td>
                </tr>
                <tr id="dealers-row-${p.id}" style="display:none;background:#f8fafc;">
                  <td colspan="8" style="padding:0">
                    <div style="padding:16px;border-bottom:1px solid #e2e8f0;">
                      <h4 style="margin:0 0 12px 0;font-size:14px;color:#475569">Bayi Fiyat ve Stok Durumu</h4>
                      <div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(250px,1fr));gap:12px;">
                        ${(p.dealerProducts || []).map(dp => `
                          <div style="background:#fff;border:1px solid #cbd5e1;padding:12px;border-radius:6px;box-shadow:0 1px 2px rgba(0,0,0,0.05)">
                            <div style="font-weight:600;margin-bottom:8px;color:#0f172a">${dp.dealer?.name || 'Bilinmeyen Bayi'}</div>
                            <div style="display:flex;justify-content:space-between;font-size:13px;margin-bottom:4px">
                              <span style="color:#64748b">Birim Fiyatı:</span>
                              <strong>${dp.unitPrice} ₺</strong>
                            </div>
                            <div style="display:flex;justify-content:space-between;font-size:13px;padding-top:6px;border-top:1px dashed #cbd5e1;margin-top:6px">
                              <span style="color:#64748b">Stok:</span>
                              <strong style="color:${dp.stockQuantity > 0 ? '#10b981' : '#ef4444'}">${dp.stockQuantity} adet</strong>
                            </div>
                          </div>
                        `).join('')}
                      </div>
                    </div>
                  </td>
                </tr>`;
      }).join('')}
            </tbody>
          </table>
        </div > `;
    }

    container.innerHTML = html;
    updateBulkActionBar();
  } catch (e) {
    toast('Ürünler yüklenemedi: ' + e.message, 'danger');
  }
}

// BULK SELECTION LOGIC
function toggleProductSelection(checkbox) {
  if (checkbox.checked) {
    window.selectedProductIds.add(parseInt(checkbox.value, 10));
  } else {
    window.selectedProductIds.delete(parseInt(checkbox.value, 10));
  }
  updateBulkActionBar();
}

function toggleCatalogSelection(masterCheckbox, catalogName) {
  const safeName = catalogName.replace(/[^a-zA-Z0-9]/g, '');
  const checkboxes = document.querySelectorAll('.pcb-' + safeName);
  
  checkboxes.forEach(cb => {
    cb.checked = masterCheckbox.checked;
    if (masterCheckbox.checked) {
      window.selectedProductIds.add(parseInt(cb.value, 10));
    } else {
      window.selectedProductIds.delete(parseInt(cb.value, 10));
    }
  });
  
  updateBulkActionBar();
}

function clearProductSelection() {
  window.selectedProductIds.clear();
  document.querySelectorAll('input[type="checkbox"]').forEach(cb => cb.checked = false);
  updateBulkActionBar();
}

function updateBulkActionBar() {
  const bar = document.getElementById('bulk-action-bar');
  const countSpan = document.getElementById('bulk-selection-count');
  
  if (!window.selectedProductIds) window.selectedProductIds = new Set();
  
  const count = window.selectedProductIds.size;
  if (count > 0) {
    bar.style.display = 'flex';
    countSpan.textContent = `${count} ürün seçildi`;
  } else {
    bar.style.display = 'none';
    const select = document.getElementById('bulk-action-select');
    if(select) select.value = "";
  }
}

async function executeBulkAction() {
  const select = document.getElementById('bulk-action-select');
  const action = select.value;
  select.value = ""; // Reset immediately
  
  if (!action || window.selectedProductIds.size === 0) return;
  
  const ids = Array.from(window.selectedProductIds);

  if (action === 'delete') {
    if (confirm(`Seçilen ${ids.length} ürünü KALICI olarak silmek istediğinize emin misiniz?`)) {
      try {
        await api('/products/bulk/delete', { method: 'POST', body: JSON.stringify({ productIds: ids }) });
        toast(`${ids.length} ürün başarıyla silindi`, 'success');
        clearProductSelection();
        loadProducts();
      } catch (e) {
        toast('Silme işlemi başarısız: ' + e.message, 'danger');
      }
    }
  } 
  else if (action === 'update-price') {
    const pct = prompt('Fiyatı YÜZDE olarak ne kadar artırmak istiyorsunuz? (Eksiltmek için eksi değer girin, örn: "-5")');
    if (pct !== null) {
      const parsedPct = parseFloat(pct);
      if (!isNaN(parsedPct)) {
        try {
          await api('/products/bulk/update-price', { method: 'POST', body: JSON.stringify({ productIds: ids, percentage: parsedPct }) });
          toast('Fiyatlar güncellendi', 'success');
          loadProducts();
        } catch(e) {
          toast('Fiyat güncelleme başarısız: ' + e.message, 'danger');
        }
      } else {
         toast('Geçersiz yüzde değeri', 'danger');
      }
    }
  }
  else if (action === 'change-collection') {
    openBulkCollectionModal(ids);
  }
  else if (action === 'assign-image') {
    openBulkImageModal(ids);
  }
  else if (action === 'export-pdf') {
    const url = `/api/products/export-pdf?selectedIds=${ids.join(',')}`;
    window.open(url, '_blank');
    clearProductSelection();
  }
  else if (action === 'export-excel') {
    exportSelectedToExcel(ids);
  }
}

async function openBulkCollectionModal(ids) {
  closeModal(); // Ensure other modals are closed
  
  try {
    const brands = await api('/brands');
    openModal('Koleksiyon Değiştir', `
      <div class="form-group">
        <label>Marka (Kataloğu Filtrelemek İçin)</label>
        <select class="form-control" id="bulk-brand-id" onchange="onBulkBrandChange()">
           <option value="">— Marka Seçin —</option>
           ${brands.map(b => `<option value="${b.id}">${b.name}</option>`).join('')}
        </select>
      </div>
      <div class="form-group">
        <label>Katalog</label>
        <select class="form-control" id="bulk-catalog-id" onchange="onBulkCatalogChange()" disabled>
           <option value="">— Önce Marka Seçin —</option>
        </select>
      </div>
      <div class="form-group">
        <label>Yeni Koleksiyon</label>
        <select class="form-control" id="bulk-collection-id" disabled>
           <option value="">— Önce Katalog Seçin —</option>
        </select>
      </div>
      
      <div style="display:flex;justify-content:flex-end;gap:8px;margin-top:16px">
        <button class="btn btn-ghost" onclick="closeModal()">İptal</button>
        <button class="btn btn-primary" onclick="submitBulkCollectionChange([${ids.join(',')}])">Uygula</button>
      </div>
    `);
  } catch (e) {
    toast('Markalar yüklenemedi: ' + e.message, 'danger');
  }
}

window.onBulkBrandChange = async function() {
  const bid = document.getElementById('bulk-brand-id').value;
  const cats = document.getElementById('bulk-catalog-id');
  const cols = document.getElementById('bulk-collection-id');
  
  cats.innerHTML = '<option value="">— Katalog Seçin —</option>';
  cols.innerHTML = '<option value="">— Önce Katalog Seçin —</option>';
  cats.disabled = true; cols.disabled = true;
  
  if(bid) {
    try {
      const list = await api(`/catalogs?brandId=${bid}`);
      cats.innerHTML += list.map(c => `<option value="${c.id}">${c.catalogName}</option>`).join('');
      cats.disabled = false;
    } catch(e) {}
  }
};

window.onBulkCatalogChange = async function() {
  const cid = document.getElementById('bulk-catalog-id').value;
  const cols = document.getElementById('bulk-collection-id');
  
  cols.innerHTML = '<option value="">— Opsiyonel Koleksiyon —</option>';
  cols.disabled = true;
  
  if(cid) {
    try {
      const list = await api(`/collections?catalogId=${cid}`);
      cols.innerHTML += list.map(c => `<option value="${c.id}">${c.collectionName}</option>`).join('');
      cols.disabled = false;
    } catch(e) {}
  }
};

window.submitBulkCollectionChange = async function(ids) {
  const colIdStr = document.getElementById('bulk-collection-id').value;
  // allow null collection
  const colId = colIdStr ? parseInt(colIdStr, 10) : null;
  
  try {
    await api('/products/bulk/update-collection', { method: 'POST', body: JSON.stringify({ productIds: ids, newCollectionId: colId }) });
    toast('Koleksiyon güncellendi', 'success');
    closeModal();
    loadProducts();
  } catch(e) {
    toast('Hata: ' + e.message, 'danger');
  }
};

function openBulkImageModal(ids) {
  closeModal();
  openModal('Toplu Görsel Ata', `
    <div style="margin-bottom:16px;font-size:14px;color:#475569">
      Seçilen <strong>${ids.length}</strong> ürüne aynı görseli uygulamak üzeresiniz.
    </div>
    <div class="form-group">
      <label>Görsel Dosyası</label>
      <input type="file" id="bulk-image-file" class="form-control" accept=".jpg,.jpeg,.png,.webp" style="padding:4px 8px" required>
    </div>
    <div style="display:flex;justify-content:flex-end;gap:8px;margin-top:16px">
      <button class="btn btn-ghost" onclick="closeModal()">İptal</button>
      <button class="btn btn-primary" onclick="submitBulkImageChange([${ids.join(',')}])">Yükle ve Ata</button>
    </div>
  `);
}

window.submitBulkImageChange = async function(ids) {
  const fileInput = document.getElementById('bulk-image-file');
  if(!fileInput.files.length) {
    toast('Lütfen bir görsel seçin', 'danger');
    return;
  }
  
  const fd = new FormData();
  fd.append('productIdsCsv', ids.join(','));
  fd.append('image', fileInput.files[0]);
  
  try {
    const res = await api('/products/bulk/upload-image', { method: 'POST', body: fd });
    toast(res.message || 'Görsel atandı', 'success');
    closeModal();
    loadProducts();
  } catch(e) {
    toast('Hata: ' + e.message, 'danger');
  }
};

async function exportSelectedToExcel(ids) {
  toast('Veriler çekiliyor, Excel indirilecek...', 'info');
  try {
    // We fetch products and manually generate a local CSV as requested
    const productDataPromises = ids.map(id => api(`/products/${id}`).catch(()=>null));
    const products = (await Promise.all(productDataPromises)).filter(p => p !== null);
    
    if(products.length === 0) {
      toast('Ürün verisi bulunamadı.', 'danger');
      return;
    }
    
    // Create CSV content (UTF-8 with BOM for Excel)
    let csvContent = "data:text/csv;charset=utf-8,\uFEFF";
    
    // Header
    const headers = ["Marka", "Katalog", "Koleksiyon", "Ad", "Kalıp Kodu", "Ürün Kodu", "Barkod", "Alış Fiyati", "Satış Fiyatı", "Liste Fiyatı"];
    csvContent += headers.join(";") + "\\r\\n";
    
    products.forEach(p => {
       const b = p.catalog?.brand?.name || '';
       const cat = p.catalog?.catalogName || '';
       const col = p.collection?.collectionName || '';
       const row = [
         `"${b}"`, `"${cat}"`, `"${col}"`, `"${p.productName.replace(/"/g, '""')}"`, `"${p.moldCode || ''}"`, `"${p.productCode || ''}"`, `"${p.barcode || ''}"`,
         p.purchasePrice, p.salePrice, p.listPrice
       ];
       csvContent += row.join(";") + "\r\n";
    });
    
    const encodedUri = encodeURI(csvContent);
    const link = document.createElement("a");
    link.setAttribute("href", encodedUri);
    link.setAttribute("download", `secilen-urunler-${new Date().toISOString().slice(0,10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    
    toast('İndirme başladı', 'success');
    clearProductSelection();
  } catch(e) {
    toast('Excel dışa aktarım hatası: ' + e.message, 'danger');
  }
}

function showProductDealers(productId) {
  const row = document.getElementById(`dealers-row-${productId}`);
  if (row) {
    row.style.display = row.style.display === 'none' ? 'table-row' : 'none';
  }
}

async function openProductModal(product = null) {
  try {
    const brands = await api('/brands');
    let catalogsHTML = '<option value="">— Önce Marka Seçin —</option>';
    let currentBrandId = product?.catalog?.brandId || '';

    // If editing, load catalogs for the current brand
    if (product && currentBrandId) {
      const catalogs = await api(`/catalogs?brandId=${currentBrandId}`);
      catalogsHTML = catalogs.map(c => `<option value="${c.id}" ${product.catalogId == c.id ? 'selected' : ''}>${c.catalogName}</option>`).join('');
    }

    openModal(product ? 'Ürün Düzenle' : 'Ürün Ekle', `
      <form id="product-form">
        <div style="display:flex;flex-direction:column;gap:16px;margin-bottom:16px">

          <!-- SECTION: GENERAL INFORMATION -->
          <div style="background:#fff;padding:16px;border:1px solid #e2e8f0;border-radius:8px">
            <h4 style="margin:0 0 12px 0;font-size:14px;color:#475569;border-bottom:1px solid #cbd5e1;padding-bottom:6px;">Genel Bilgiler</h4>
            <div class="form-row">
              <div class="form-group" style="width:100%">
                <label>Ürün Adı *</label>
                <input class="form-control" name="productName" value="${product?.productName || ''}" placeholder="ör. Coca Cola 330ml" required>
              </div>
            </div>
            <div class="form-row" id="code-fields-row" style="display:none;background:#f8fafc;padding:12px;border-radius:6px;border:1px solid #e2e8f0;margin-top:8px;">
              <div class="form-group" id="group-mold-code" style="display:none;">
                <label>Kalıp Kodu *</label>
                <input class="form-control" name="moldCode" id="input-mold-code" value="${product?.moldCode || ''}" placeholder="ör. B12">
              </div>
              <div class="form-group" id="group-product-code" style="display:none;">
                <label>Ürün Kodu *</label>
                <input class="form-control" name="productCode" id="input-product-code" value="${product?.productCode || ''}" placeholder="ör. CC330">
              </div>
              <div class="form-group" id="group-barcode" style="display:none;">
                <label>Barkod *</label>
                <input class="form-control" name="barcode" id="input-barcode" value="${product?.barcode || ''}" placeholder="ör. 869...">
              </div>
              <div id="code-structure-hint" style="width:100%;font-size:12px;color:#64748b;margin-top:4px;"></div>
            </div>
          </div>

          <!-- SECTION: STRUCTURE -->
          <div style="background:#fff;padding:16px;border:1px solid #e2e8f0;border-radius:8px">
            <h4 style="margin:0 0 12px 0;font-size:14px;color:#475569;border-bottom:1px solid #cbd5e1;padding-bottom:6px;">Yapı</h4>
            <div class="form-row">
              <div class="form-group">
                <label>Marka *</label>
                <select class="form-control" id="form-brand-id" onchange="onFormBrandChange(this)" required>
                  <option value="" data-structure="">— Marka Seçin —</option>
                  ${brands.map(b => `<option value="${b.id}" data-structure="${b.codeStructure}" ${currentBrandId == b.id ? 'selected' : ''}>${b.name}</option>`).join('')}
                </select>
              </div>
              <div class="form-group">
                <label>Katalog *</label>
                <select class="form-control" name="catalogId" id="form-catalog-id" onchange="onFormCatalogChange(this)" required ${!currentBrandId ? 'disabled' : ''}>
                  ${!currentBrandId ? '<option value="">— Önce Marka Seçin —</option>' : catalogsHTML}
                </select>
              </div>
              <div class="form-group">
                <label>Koleksiyon</label>
                <select class="form-control" name="collectionId" id="form-collection-id" ${!product?.catalogId ? 'disabled' : ''}>
                   <option value="">— Koleksiyon Seçin —</option>
                   <!-- Populated dynamically -->
                </select>
              </div>
            </div>
          </div>

          <!-- SECTION: PRICING -->
          <div style="background:#fff;padding:16px;border:1px solid #e2e8f0;border-radius:8px">
            <h4 style="margin:0 0 12px 0;font-size:14px;color:#475569;border-bottom:1px solid #cbd5e1;padding-bottom:6px;">Fiyatlandırma</h4>
            <div class="form-row">
              <div class="form-group">
                <label>Alış Fiyatı (₺)</label>
                <input class="form-control" name="purchasePrice" type="number" step="0.01" min="0" value="${product?.purchasePrice ?? '0'}">
              </div>
              <div class="form-group">
                <label>Satış Fiyatı (₺)</label>
                <input class="form-control" name="salePrice" type="number" step="0.01" min="0" value="${product?.salePrice ?? '0'}">
              </div>
              <div class="form-group">
                <label>Liste Fiyatı (₺)</label>
                <input class="form-control" name="listPrice" type="number" step="0.01" min="0" value="${product?.listPrice ?? '0'}">
              </div>
            </div>
          </div>

          <!-- SECTION: PACKAGING -->
          <div style="background:#fff;padding:16px;border:1px solid #e2e8f0;border-radius:8px">
            <h4 style="margin:0 0 12px 0;font-size:14px;color:#475569;border-bottom:1px solid #cbd5e1;padding-bottom:6px;">Paketleme</h4>
            <div class="form-row">
              <div class="form-group">
                <label>Kategori (Paket Türü)</label>
                <input class="form-control" name="packageType" value="${product?.packageType || ''}" placeholder="ör. İçecek">
              </div>
              <div class="form-group">
                <label>Koli Boyutu (Koli içi adet)</label>
                <input class="form-control" name="unitsPerCase" type="number" min="1" value="${product?.unitsPerCase ?? ''}" placeholder="ör. 24">
              </div>
              <div class="form-group">
                <label>Paket Boyutu (Paket içi adet)</label>
                <input class="form-control" name="unitsPerPack" type="number" min="1" value="${product?.unitsPerPack ?? ''}" placeholder="ör. 6">
              </div>
            </div>
          </div>

          <!-- SECTION: IMAGE -->
          <div style="background:#fff;padding:16px;border:1px solid #e2e8f0;border-radius:8px">
            <h4 style="margin:0 0 12px 0;font-size:14px;color:#475569;border-bottom:1px solid #cbd5e1;padding-bottom:6px;">Ürün Görseli</h4>
            <div style="display:flex;gap:16px;align-items:center;">
              <div style="width:100px;">
                ${product?.imageUrl
        ? `<img id="form-image-preview" src="${product.imageUrl}" style="width:100px;height:100px;object-fit:cover;border-radius:8px;border:1px solid #e2e8f0">`
        : `<div id="form-image-preview" style="width:100px;height:100px;background:#f1f5f9;border-radius:8px;border:1px solid #e2e8f0;display:flex;align-items:center;justify-content:center;color:#94a3b8;font-size:12px;text-align:center">Görsel<br>Yok</div>`
      }
              </div>
              <div style="flex:1;">
                <label class="btn btn-outline btn-sm" style="cursor:pointer;margin-bottom:8px;display:inline-block;">
                  Yeni Görsel Seç
                  <input type="file" name="ImageFile" id="form-image-file" accept=".jpg,.jpeg,.png,.webp" style="display:none" onchange="previewProductImage(event)">
                </label>
                ${product?.imageUrl ? `<br><button type="button" class="btn btn-danger btn-sm" onclick="removeProductImage(${product.id})">Mevcut Görseli Sil</button>` : ''}
              </div>
            </div>
          </div>
        </div>
        <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:16px">
          <button type="button" class="btn btn-ghost" onclick="closeModal()">İptal</button>
          <button type="submit" class="btn btn-primary">${product ? 'Değişiklikleri Kaydet' : 'Ürün Oluştur'}</button>
        </div>
      </form>`);

    document.getElementById('product-form').addEventListener('submit', async e => {
      e.preventDefault();
      const fd = new FormData(e.target);
      const csVal = fd.get('unitsPerCase');
      const psVal = fd.get('unitsPerPack');
      const catId = fd.get('catalogId');
      const collId = fd.get('collectionId');

      const body = {
        productName: fd.get('productName'),
        productCode: fd.get('productCode') || null,
        moldCode: fd.get('moldCode') || null,
        barcode: fd.get('barcode') || null,
        catalogId: catId ? parseInt(catId, 10) : null,
        collectionId: collId ? parseInt(collId, 10) : null,
        packageType: fd.get('packageType'),
        unitsPerCase: csVal ? parseInt(csVal, 10) : null,
        unitsPerPack: psVal ? parseInt(psVal, 10) : null,
        purchasePrice: parseFloat(fd.get('purchasePrice') || 0),
        salePrice: parseFloat(fd.get('salePrice') || 0),
        listPrice: parseFloat(fd.get('listPrice') || 0)
      };

      try {
        let savedProduct;
        if (product) {
          savedProduct = await api(`/products/${product.id}`, { method: 'PUT', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } });
          toast('Ürün güncellendi', 'success');
        } else {
          savedProduct = await api('/products', { method: 'POST', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } });
          toast('Ürün oluşturuldu', 'success');
        }

        const imageFile = document.getElementById('form-image-file').files[0];
        if (imageFile) {
          const imgFd = new FormData();
          imgFd.append('image', imageFile);
          await api(`/products/${savedProduct.id}/image`, {
            method: 'POST',
            body: imgFd
          });
          toast('Görsel yüklendi', 'success');
        }

        closeModal();
        await loadProducts();
      } catch (err) {
        toast('Ürün kaydedilemedi: ' + err.message, 'danger');
      }
    });

    // Trigger initial state if brand is already selected
    const initialBrandSelect = document.getElementById('form-brand-id');
    if (initialBrandSelect && initialBrandSelect.value) {
      await onFormBrandChange(initialBrandSelect, true);

      // Trigger collection population if catalog is pre-selected for edit
      if (product && product.catalogId) {
        const catSelect = document.getElementById('form-catalog-id');
        await loadCollectionsForCatalog(product.catalogId, product.collectionId);
      }
    }

  } catch (e) {
    toast('Ürün formu açılamadı: ' + e.message, 'danger');
  }
}

window.previewProductImage = function (event) {
  const file = event.target.files[0];
  if (file) {
    const reader = new FileReader();
    reader.onload = function (e) {
      const preview = document.getElementById('form-image-preview');
      if (preview.tagName === 'IMG') {
        preview.src = e.target.result;
      } else {
        const img = document.createElement('img');
        img.id = 'form-image-preview';
        img.src = e.target.result;
        img.style.cssText = "width:100px;height:100px;object-fit:cover;border-radius:8px;border:1px solid #e2e8f0";
        preview.parentNode.replaceChild(img, preview);
      }
    }
    reader.readAsDataURL(file);
  }
}

window.removeProductImage = async function (productId) {
  if (!confirm('Ürün görselini silmek istediğinize emin misiniz?')) return;
  try {
    await api(`/products/${productId}/image`, { method: 'DELETE' });
    toast('Görsel silindi', 'success');
    closeModal();
    openProductModal(await api(`/products/${productId}`));
  } catch (e) {
    toast('Görsel silinemedi: ' + e.message, 'danger');
  }
}

window.onFormBrandChange = async function (selectElement, skipCatalogFetch = false) {
  const brandId = selectElement.value;
  const struct = selectElement.options[selectElement.selectedIndex]?.dataset.structure || '';

  const catalogSelect = document.getElementById('form-catalog-id');
  const codeRow = document.getElementById('code-fields-row');
  const grpMold = document.getElementById('group-mold-code');
  const grpProd = document.getElementById('group-product-code');
  const grpBarc = document.getElementById('group-barcode');
  const hinText = document.getElementById('code-structure-hint');

  const inpMold = document.getElementById('input-mold-code');
  const inpProd = document.getElementById('input-product-code');
  const inpBarc = document.getElementById('input-barcode');

  // Reset visibility and required toggles
  codeRow.style.display = 'flex';
  grpMold.style.display = 'none'; inpMold.required = false;
  grpProd.style.display = 'none'; inpProd.required = false;
  grpBarc.style.display = 'none'; inpBarc.required = false;

  if (!brandId) {
    codeRow.style.display = 'none';
    catalogSelect.innerHTML = '<option value="">— Önce Marka Seçin —</option>';
    catalogSelect.disabled = true;
    return;
  }

  // Adopt Code Structure format
  if (struct === 'barcode') {
    grpBarc.style.display = 'block'; inpBarc.required = true;
    hinText.innerHTML = '📌 Bu marka <b>Barkod</b> sistemi ile ürünlerini yönetiyor.';
  } else if (struct === 'dual_code') {
    grpMold.style.display = 'block'; inpMold.required = true;
    grpProd.style.display = 'block'; inpProd.required = true;
    hinText.innerHTML = '📌 Bu marka ürünlerini <b>Kalıp Kodu + Ürün Kodu</b> kombinasyonu ile yönetiyor.';
  } else {
    // default single_code
    grpProd.style.display = 'block'; inpProd.required = true;
    hinText.innerHTML = '📌 Bu marka <b>Tekil Ürün Kodu</b> kullanıyor.';
  }

  // Catalog loading logic
  if (skipCatalogFetch) return;

  catalogSelect.innerHTML = '<option value="">Yükleniyor...</option>';
  catalogSelect.disabled = true;

  try {
    const catalogs = await api(`/catalogs?brandId=${brandId}`);
    if (catalogs.length === 0) {
      catalogSelect.innerHTML = '<option value="">— Bu markaya ait katalog yok —</option>';
    } else {
      catalogSelect.innerHTML = '<option value="">— Katalog Seçin —</option>' +
        catalogs.map(c => `<option value="${c.id}">${c.catalogName}</option>`).join('');
      catalogSelect.disabled = false;
    }
  } catch (err) {
    toast('Kataloglar yüklenemedi: ' + err.message, 'danger');
    catalogSelect.innerHTML = '<option value="">— Hata —</option>';
  }
}

async function deleteProduct(id) {
  if (!confirm('Bu ürünü silmek istiyor musunuz?')) return;
  try {
    await api(`/products/${id}`, { method: 'DELETE' });
    toast('Ürün silindi');
    await loadProducts();
  } catch (e) {
    toast('Ürün silinemedi: ' + e.message, 'danger');
  }
}

function exportProductListPdf() {
  const queryStr = getProductFiltersQuery();
  const url = queryStr ? `/api/products/export-pdf?${queryStr}` : '/api/products/export-pdf';
  window.open(url, '_blank');
}
