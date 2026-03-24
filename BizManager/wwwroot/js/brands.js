// Markalar — Logo yükleme ve oluşturulma tarihi sütunuyla tam CRUD

async function renderBrands() {
  document.getElementById('page-content').innerHTML = `
  <div class="card">
    <div class="card-header">
      <h2>Markalar</h2>
      <button class="btn btn-primary btn-sm" onclick="openBrandModal()">+ Marka Ekle</button>
    </div>
    <div class="card-body">
      <table>
        <thead><tr><th>Logo</th><th>Ad</th><th>Web Sitesi</th><th>Kod Yapısı</th><th>Açıklama</th><th>Oluşturulma</th><th>İşlemler</th></tr></thead>
        <tbody id="brands-list"><tr><td colspan="7" style="text-align:center;padding:20px;color:#94a3b8">Yükleniyor…</td></tr></tbody>
      </table>
    </div>
  </div>`;
  await loadBrands();
}

async function loadBrands() {
  try {
    const brands = await api('/brands');
    const tbody = document.getElementById('brands-list');
    if (!brands.length) {
      tbody.innerHTML = '<tr><td colspan="5"><div class="empty-state"><p>Henüz marka yok. Başlamak için "+ Marka Ekle" butonuna tıklayın.</p></div></td></tr>';
      return;
    }
    tbody.innerHTML = brands.map(b => `
      <tr>
        <td>${b.logoPath ? `<img src="${b.logoPath}" style="height:40px;width:40px;object-fit:contain;border-radius:6px;border:1px solid #e2e8f0">` : '<span style="color:#cbd5e1;font-size:12px">Logo yok</span>'}</td>
        <td><strong>${b.name}</strong></td>
        <td>${b.websiteDomain || '—'}</td>
        <td>
          <span class="badge ${b.codeStructure === 'barcode' ? 'badge-primary' : b.codeStructure === 'dual_code' ? 'badge-info' : 'badge-muted'}">
            ${b.codeStructure === 'barcode' ? 'Barkod' : b.codeStructure === 'dual_code' ? 'İkili Kod (Kalıp+Ürün)' : 'Tekil Kod'}
          </span>
        </td>
        <td>${b.description || '—'}</td>
        <td>${fmtDate(b.createdAt)}</td>
        <td>
          <div class="section-actions">
            <button class="btn btn-ghost btn-sm" onclick='openBrandModal(${JSON.stringify(b).replace(/'/g, "&#39;")})'>Düzenle</button>
            <button class="btn btn-danger btn-sm" onclick="deleteBrand(${b.id})">Sil</button>
          </div>
        </td>
      </tr>`).join('');
  } catch (e) {
    toast('Markalar yüklenemedi: ' + e.message, 'danger');
  }
}

function openBrandModal(brand = null) {
  openModal(brand ? 'Marka Düzenle' : 'Marka Ekle', `
    <form id="brand-form">
      <div class="form-group">
        <label>Marka Adı *</label>
        <input class="form-control" name="name" value="${brand?.name || ''}" placeholder="ör. Samsung, Apple" required>
      </div>
      <div class="form-group">
        <label>Web Sitesi Alan Adı (Görsel Tarama İçin)</label>
        <input class="form-control" name="websiteDomain" value="${brand?.websiteDomain || ''}" placeholder="ör. porland.com">
      </div>
      <div class="form-group">
        <label>Ürün Kodlama Yapısı *</label>
        <select class="form-control" name="codeStructure" required>
          <option value="single_code" ${brand?.codeStructure === 'single_code' ? 'selected' : ''}>Tekil Kod</option>
          <option value="dual_code" ${brand?.codeStructure === 'dual_code' ? 'selected' : ''}>İkili Kod (Kalıp Kodu + Ürün Kodu)</option>
          <option value="barcode" ${brand?.codeStructure === 'barcode' ? 'selected' : ''}>Barkod</option>
        </select>
      </div>
      <div class="form-group">
        <label>Açıklama</label>
        <textarea class="form-control" name="description" rows="3" placeholder="İsteğe bağlı marka açıklaması">${brand?.description || ''}</textarea>
      </div>
      <div class="form-group">
        <label>Logo (görsel dosyası)</label>
        ${brand?.logoPath ? `<div style="margin-bottom:8px"><img src="${brand.logoPath}" style="height:50px;border-radius:6px"></div>` : ''}
        <input type="file" class="form-control" name="logo" accept="image/*">
      </div>
        ${brand ? `
        <div style="margin-top:24px;border-top:1px solid var(--border);padding-top:16px;">
          <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:12px">
            <h4 style="margin:0;font-size:14px">Kataloglar</h4>
            <button type="button" class="btn btn-primary btn-sm" onclick="openCatalogUploadModal(${brand.id})">Katalog Yükle</button>
          </div>
          <div class="items-table-wrapper">
            <table>
              <thead><tr><th>Katalog Adı</th><th>Orijinal Dosya</th><th>Tarih</th><th>İşlemler</th></tr></thead>
              <tbody id="catalogs-list-${brand.id}"><tr><td colspan="4" style="text-align:center;color:#94a3b8">Kataloglar yükleniyor...</td></tr></tbody>
            </table>
          </div>
        </div>
        ` : ''}
      <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:16px">
        <button type="button" class="btn btn-ghost" onclick="closeModal()">İptal</button>
        <button type="submit" class="btn btn-primary">${brand ? 'Değişiklikleri Kaydet' : 'Marka Oluştur'}</button>
      </div>
    </form>`);

  if (brand) {
    loadBrandCatalogs(brand.id);
  }

  document.getElementById('brand-form').addEventListener('submit', async e => {
    e.preventDefault();
    const fd = new FormData(e.target);
    if (!fd.get('logo')?.size) fd.delete('logo');
    try {
      if (brand) {
        await api(`/brands/${brand.id}`, { method: 'PUT', body: fd });
        toast('Marka güncellendi', 'success');
      } else {
        await api('/brands', { method: 'POST', body: fd });
        toast('Marka oluşturuldu', 'success');
      }
      closeModal();
      await loadBrands();
    } catch (e) {
      toast('Marka kaydedilemedi: ' + e.message, 'danger');
    }
  });
}

async function deleteBrand(id) {
  if (!confirm('Bu markayı silmek istiyor musunuz? İlişkili bayiler ve ürünler etkilenecektir.')) return;
  try {
    await api(`/brands/${id}`, { method: 'DELETE' });
    toast('Marka silindi');
    await loadBrands();
  } catch (e) {
    toast('Marka silinemedi: ' + e.message, 'danger');
  }
}

// ── Marka Katalogları ────────────────────────────────────────────────────────
async function loadBrandCatalogs(brandId) {
  const tbody = document.getElementById(`catalogs-list-${brandId}`);
  if (!tbody) return;
  try {
    const catalogs = await api(`/brands/${brandId}/catalogs`);
    if (!catalogs.length) {
      tbody.innerHTML = '<tr><td colspan="4" style="text-align:center;color:#94a3b8">Bu markaya ait katalog bulunamadı.</td></tr>';
      return;
    }
    tbody.innerHTML = catalogs.map(c => `
      <tr>
        <td><strong>${c.customFileName}</strong></td>
        <td style="color:var(--text-muted);font-size:12px">${c.originalFileName}</td>
        <td>${fmtDate(c.uploadedAt)}</td>
        <td>
          <div class="section-actions" style="flex-wrap:nowrap">
            <a href="${c.filePath}" target="_blank" class="btn btn-success btn-sm">İndir</a>
            <button type="button" class="btn btn-ghost btn-sm" onclick="promptRenameCatalog(${brandId}, ${c.id}, '${c.customFileName.replace(/'/g, "\\'")}')">Yeniden Adlandır</button>
            <button type="button" class="btn btn-danger btn-sm" onclick="deleteCatalog(${brandId}, ${c.id})">Sil</button>
          </div>
        </td>
      </tr>`).join('');
  } catch (e) {
    tbody.innerHTML = '<tr><td colspan="4" style="color:red">Kataloglar yüklenemedi.</td></tr>';
  }
}

function openCatalogUploadModal(brandId) {
  // Store previous modal content to restore later (nested modal logic replacement)
  const prevTitle = document.getElementById('modal-title').textContent;
  const prevBody = document.getElementById('modal-body').innerHTML;

  openModal('Katalog Yükle', `
    <form id="catalog-upload-form">
      <div class="form-group">
        <label>Katalog Dosyası *</label>
        <input type="file" id="catalog-file" class="form-control" required>
      </div>
      <div class="form-group">
        <label>Katalog Adı (İsteğe bağlı)</label>
        <input type="text" id="catalog-custom-name" class="form-control" placeholder="Örn: 2025 Bahar Kataloğu">
      </div>
      <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:16px">
        <button type="button" class="btn btn-ghost" id="btn-cancel-upload">İptal</button>
        <button type="submit" class="btn btn-primary" id="btn-upload-submit">Yükle</button>
      </div>
    </form>
  `);

  document.getElementById('btn-cancel-upload').onclick = () => {
    // Restore previous modal
    document.getElementById('modal-title').textContent = prevTitle;
    document.getElementById('modal-body').innerHTML = prevBody;
    // We must re-bind the main form submit event and reload catalogs
    bindMainBrandForm(brandId);
    loadBrandCatalogs(brandId);
  };

  document.getElementById('catalog-upload-form').onsubmit = async (e) => {
    e.preventDefault();
    const fileInput = document.getElementById('catalog-file');
    const customName = document.getElementById('catalog-custom-name').value;

    if (!fileInput.files[0]) return;

    const fd = new FormData();
    fd.append('file', fileInput.files[0]);
    fd.append('customFileName', customName);

    const btn = document.getElementById('btn-upload-submit');
    btn.disabled = true;
    btn.textContent = 'Yükleniyor...';

    try {
      const res = await fetch(`/api/brands/${brandId}/catalogs`, { method: 'POST', body: fd });
      if (!res.ok) throw new Error(await res.text() || 'Yükleme başarısız');
      toast('Katalog başarıyla yüklendi.', 'success');

      // Restore previous modal and refresh catalogs
      document.getElementById('modal-title').textContent = prevTitle;
      document.getElementById('modal-body').innerHTML = prevBody;
      bindMainBrandForm(brandId);
      loadBrandCatalogs(brandId);
    } catch (err) {
      toast(err.message, 'danger');
      btn.disabled = false;
      btn.textContent = 'Yükle';
    }
  };
}

// Helper to re-bind the brand form after returning from the nested catalog upload modal
function bindMainBrandForm(brandId) {
  const form = document.getElementById('brand-form');
  if (form) {
    form.addEventListener('submit', async e => {
      e.preventDefault();
      const fd = new FormData(e.target);
      if (!fd.get('logo')?.size) fd.delete('logo');
      try {
        await api(`/brands/${brandId}`, { method: 'PUT', body: fd });
        toast('Marka güncellendi', 'success');
        closeModal();
        await loadBrands();
      } catch (err) {
        toast('Marka kaydedilemedi: ' + err.message, 'danger');
      }
    });
  }
}

async function promptRenameCatalog(brandId, catalogId, oldName) {
  const newName = prompt('Katalog için yeni bir ad girin:', oldName);
  if (!newName || newName === oldName) return;

  try {
    await api(`/brands/${brandId}/catalogs/${catalogId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ newName })
    });
    toast('Katalog adı güncellendi.', 'success');
    loadBrandCatalogs(brandId);
  } catch (e) {
    toast('Yeniden adlandırılamadı: ' + e.message, 'danger');
  }
}

async function deleteCatalog(brandId, catalogId) {
  if (!confirm('Bu kataloğu tamamen silmek istediğinizden emin misiniz?')) return;
  try {
    await api(`/brands/${brandId}/catalogs/${catalogId}`, { method: 'DELETE' });
    toast('Katalog silindi.', 'success');
    loadBrandCatalogs(brandId);
  } catch (e) {
    toast('Katalog silinemedi: ' + e.message, 'danger');
  }
}

