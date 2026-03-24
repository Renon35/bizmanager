// Kataloglar — Ürün Katalogları Yönetimi

async function renderCatalogs() {
  document.getElementById('page-content').innerHTML = `
  <div class="card">
    <div class="card-header">
      <h2>Kataloglar</h2>
      <button class="btn btn-primary btn-sm" onclick="openCatalogModal()">+ Katalog Ekle</button>
    </div>
    <div class="card-body">
      <table>
        <thead><tr><th>Katalog Adı</th><th>Bağlı Marka</th><th>Açıklama</th><th>Oluşturulma</th><th>İşlemler</th></tr></thead>
        <tbody id="catalogs-page-list"><tr><td colspan="5" style="text-align:center;padding:20px;color:#94a3b8">Yükleniyor…</td></tr></tbody>
      </table>
    </div>
  </div>`;
  await loadPageCatalogs();
}

async function loadPageCatalogs() {
  try {
    const catalogs = await api('/catalogs');
    const tbody = document.getElementById('catalogs-page-list');
    if (!catalogs.length) {
      tbody.innerHTML = '<tr><td colspan="5"><div class="empty-state"><p>Henüz katalog yok. Başlamak için "+ Katalog Ekle" butonuna tıklayın.</p></div></td></tr>';
      return;
    }
    tbody.innerHTML = catalogs.map(c => `
      <tr>
        <td><strong>${c.catalogName}</strong></td>
        <td>${c.brandName || '<span style="color:#94a3b8">Yok</span>'}</td>
        <td>${c.description || '—'}</td>
        <td>${fmtDate(c.createdAt)}</td>
        <td>
          <div class="section-actions">
            <button class="btn btn-ghost btn-sm" onclick='openCatalogModal(${JSON.stringify(c).replace(/'/g, "&#39;")})'>Düzenle</button>
            <button class="btn btn-danger btn-sm" onclick="deleteProductCatalog(${c.id})">Sil</button>
          </div>
        </td>
      </tr>`).join('');
  } catch (e) {
    toast('Kataloglar yüklenemedi: ' + e.message, 'danger');
  }
}

async function openCatalogModal(catalog = null) {
  try {
    const brands = await api('/brands');
    
    openModal(catalog ? 'Katalog Düzenle' : 'Katalog Ekle', `
      <form id="catalog-page-form">
        <div class="form-group">
          <label>Katalog Adı *</label>
          <input class="form-control" name="catalogName" value="${catalog?.catalogName || ''}" placeholder="ör. 2026 İlkbahar" required>
        </div>
        <div class="form-group">
          <label>Marka *</label>
          <select class="form-control" name="brandId" required>
            <option value="">— Marka Seçin —</option>
            ${brands.map(b => `<option value="${b.id}" ${catalog && catalog.brandId == b.id ? 'selected' : ''}>${b.name}</option>`).join('')}
          </select>
        </div>
        <div class="form-group">
          <label>Açıklama</label>
          <textarea class="form-control" name="description" rows="3" placeholder="İsteğe bağlı katalog açıklaması">${catalog?.description || ''}</textarea>
        </div>
        <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:16px">
          <button type="button" class="btn btn-ghost" onclick="closeModal()">İptal</button>
          <button type="submit" class="btn btn-primary">${catalog ? 'Değişiklikleri Kaydet' : 'Katalog Oluştur'}</button>
        </div>
      </form>`);

    document.getElementById('catalog-page-form').addEventListener('submit', async e => {
      e.preventDefault();
      const fd = new FormData(e.target);
      const data = {
        catalogName: fd.get('catalogName'),
        brandId: parseInt(fd.get('brandId')),
        description: fd.get('description')
      };
      
      try {
        if (catalog) {
          await api(`/catalogs/${catalog.id}`, { method: 'PUT', body: JSON.stringify(data), headers: { 'Content-Type': 'application/json' } });
          toast('Katalog güncellendi', 'success');
        } else {
          await api('/catalogs', { method: 'POST', body: JSON.stringify(data), headers: { 'Content-Type': 'application/json' } });
          toast('Katalog oluşturuldu', 'success');
        }
        closeModal();
        await loadPageCatalogs();
      } catch (err) {
        toast('Katalog kaydedilemedi: ' + err.message, 'danger');
      }
    });
  } catch (e) {
    toast('Markalar yüklenirken hata oluştu.', 'danger');
  }
}

async function deleteProductCatalog(id) {
  if (!confirm('Bu kataloğu silmek istiyor musunuz? İlgili ürünler etkilenebilir.')) return;
  try {
    await api(`/catalogs/${id}`, { method: 'DELETE' });
    toast('Katalog silindi');
    await loadPageCatalogs();
  } catch (e) {
    toast('Katalog silinemedi: ' + e.message, 'danger');
  }
}
