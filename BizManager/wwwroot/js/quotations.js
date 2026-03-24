// Teklifler — Dinamik kalem oluşturucu ve PDF indirme ile tam CRUD

let qtItems = [];

async function renderQuotations() {
  document.getElementById('page-content').innerHTML = `
  <div class="card">
    <div class="card-header">
      <h2>Teklifler</h2>
      <button class="btn btn-primary btn-sm" onclick="openQtModal()">+ Yeni Teklif</button>
    </div>
    <div class="card-body">
      <table>
        <thead><tr><th>Teklif #</th><th>Müşteri</th><th>Satış Temsilcisi</th><th>Tarih</th><th>Kalemler</th><th>Toplam</th><th>İşlemler</th></tr></thead>
        <tbody id="qt-list"><tr><td colspan="7" style="text-align:center;padding:20px;color:#94a3b8">Yükleniyor…</td></tr></tbody>
      </table>
    </div>
  </div>`;
  await loadQuotations();
}

async function loadQuotations() {
  try {
    const quotations = await api('/quotations');
    const tbody = document.getElementById('qt-list');
    if (!quotations.length) {
      tbody.innerHTML = '<tr><td colspan="7"><div class="empty-state"><p>Henüz teklif yok. Oluşturmak için "+ Yeni Teklif" butonuna tıklayın.</p></div></td></tr>';
      return;
    }
    tbody.innerHTML = quotations.map(q => `
      <tr>
        <td><strong>${q.quotationNumber}</strong></td>
        <td>${q.customer?.companyName || '—'}</td>
        <td>${q.salesRep ? `${q.salesRep.firstName} ${q.salesRep.lastName}` : '—'}</td>
        <td>${fmtDate(q.date)}</td>
        <td>${(q.items || []).length} kalem</td>
        <td><strong>₺${fmtMoney(q.totalPrice)}</strong></td>
        <td>
          <div class="section-actions">
            <button class="btn btn-primary btn-sm" onclick="convertToSalesOrder(${q.id})">Siparişe Aktar</button>
            <a class="btn btn-success btn-sm" href="/api/quotations/${q.id}/pdf" target="_blank">📄 PDF</a>
            <button class="btn btn-ghost btn-sm" onclick="editQt(${q.id})">Düzenle</button>
            <button class="btn btn-danger btn-sm" onclick="deleteQt(${q.id})">Sil</button>
          </div>
        </td>
      </tr>`).join('');
  } catch (e) {
    toast('Teklifler yüklenemedi: ' + e.message, 'danger');
  }
}

async function editQt(id) {
  const qt = await api(`/quotations/${id}`);
  openQtModal(qt);
}

async function openQtModal(qt = null) {
  try {
    const [reps, customers, dealers] = await Promise.all([api('/sales-reps'), api('/customers'), api('/dealers')]);

    qtItems = (qt?.items || []).map(i => ({
      productName: i.productName, 
      productCode: i.productCode || '',
      moldCode: i.moldCode || '',
      barcode: i.barcode || '',
      quantity: i.quantity, unitPrice: i.unitPrice, imageUrl: i.imageUrl,
      basePrices: { purchasePrice: i.unitPrice, salePrice: i.unitPrice, listPrice: i.unitPrice },
      manualPrice: true
    }));

    openModal(qt ? 'Teklif Düzenle' : 'Yeni Teklif', `
      <form id="qt-form">
        <div class="form-row">
          <div class="form-group">
            <label>Teklif Numarası *</label>
            <input class="form-control" name="quotationNumber" value="${qt?.quotationNumber || ''}" placeholder="ör. TKL-2026-001" required>
          </div>
          <div class="form-group">
            <label>Tarih</label>
            <input class="form-control" name="date" type="date"
              value="${qt ? new Date(qt.date).toISOString().split('T')[0] : new Date().toISOString().split('T')[0]}">
          </div>
        </div>
        <div class="form-row">
          <div class="form-group">
            <label>Satış Temsilcisi *</label>
            <select class="form-control" name="salesRepId" required>
              <option value="">— Temsilci Seçin —</option>
              ${reps.map(r => `<option value="${r.id}" ${qt?.salesRepId == r.id ? 'selected' : ''}>${r.firstName} ${r.lastName}</option>`).join('')}
            </select>
          </div>
          <div class="form-group">
            <label>Müşteri *</label>
            <select class="form-control" name="customerId" required>
              <option value="">— Müşteri Seçin —</option>
              ${customers.map(c => `<option value="${c.id}" ${qt?.customerId == c.id ? 'selected' : ''}>${c.companyName}</option>`).join('')}
            </select>
          </div>
          <div class="form-group">
            <label>Bayi *</label>
            <select class="form-control" name="dealerId" id="qt-dealer-id" required>
              <option value="">— Bayi Seçin —</option>
              ${dealers.map(d => `<option value="${d.id}" ${qt?.dealerId == d.id ? 'selected' : ''}>${d.name}</option>`).join('')}
            </select>
          </div>
        </div>
        <div class="form-row">
          <div class="form-group">
            <label>Fiyat Listesi Seçimi</label>
            <select class="form-control" id="qt-price-list" onchange="applyGlobalPriceChange()">
              <option value="SalePrice">Satış Fiyatı</option>
              <option value="PurchasePrice">Alış Fiyatı</option>
              <option value="ListPrice">Liste Fiyatı</option>
            </select>
          </div>
          <div class="form-group">
            <label>Fiyat Ayarlaması (% İndirim/Artış)</label>
            <input class="form-control" id="qt-price-adj" type="number" step="0.01" value="0" placeholder="Örn: -10 veya 5" oninput="applyGlobalPriceChange()">
          </div>
        </div>
        <div style="margin:12px 0 6px"><strong>Teklif Kalemleri</strong></div>
        <div style="display:grid;grid-template-columns:40px 2fr 130px 90px 100px 70px 30px;gap:6px;margin-bottom:4px;font-size:11px;font-weight:600;color:#64748b;text-transform:uppercase">
          <span></span><span>Ürün Adı</span><span>Kod / Barkod</span><span>Adet</span><span>Birim Fiyat</span><span></span><span></span>
        </div>
        <div id="qt-items-list"></div>
        <button type="button" class="btn btn-ghost btn-sm" style="margin-top:8px" onclick="addQtItem()">+ Kalem Ekle</button>
        <div id="qt-total" style="text-align:right;font-weight:600;margin-top:10px;font-size:15px;color:#1a56db"></div>
        <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:16px">
          <button type="button" class="btn btn-ghost" onclick="closeModal()">İptal</button>
          <button type="submit" class="btn btn-primary">${qt ? 'Değişiklikleri Kaydet' : 'Teklif Oluştur'}</button>
        </div>
      </form>`);

    renderQtItems();

    document.getElementById('qt-form').addEventListener('submit', async e => {
      e.preventDefault();
      const fd = new FormData(e.target);
      const subtotal = qtItems.reduce((s, i) => s + (+i.quantity * +i.unitPrice), 0);
      const vatRate = 20.0;
      const vatAmount = subtotal * (vatRate / 100);
      const grandTotal = subtotal + vatAmount;

      const body = {
        quotationNumber: fd.get('quotationNumber'),
        salesRepId: +fd.get('salesRepId'),
        customerId: +fd.get('customerId'),
        dealerId: +fd.get('dealerId'),
        date: new Date(fd.get('date')).toISOString(),
        vatRate: vatRate,
        items: qtItems.map(i => ({
          productName: i.productName,
          productCode: i.productCode || null,
          moldCode: i.moldCode || null,
          barcode: i.barcode || null,
          quantity: +i.quantity,
          unitPrice: +i.unitPrice,
          totalPrice: +(+i.quantity * +i.unitPrice).toFixed(2),
          imageUrl: i.imageUrl || null
        }))
      };
      try {
        if (qt) {
          await api(`/quotations/${qt.id}`, { method: 'PUT', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } });
          toast('Teklif güncellendi', 'success');
        } else {
          await api('/quotations', { method: 'POST', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } });
          toast('Teklif oluşturuldu', 'success');
        }
        closeModal();
        await loadQuotations();
      } catch (err) {
        toast('Teklif kaydedilemedi: ' + err.message, 'danger');
      }
    });
  } catch (e) {
    toast('Teklif formu açılamadı: ' + e.message, 'danger');
  }
}

function addQtItem() {
  qtItems.push({ productName: '', productCode: '', moldCode: '', barcode: '', quantity: 1, unitPrice: 0, imageUrl: null, basePrices: { purchasePrice: 0, salePrice: 0, listPrice: 0 }, manualPrice: false });
  renderQtItems();
}

function removeQtItem(i) {
  qtItems.splice(i, 1);
  renderQtItems();
}

function updateQtTotal() {
  const subtotal = qtItems.reduce((s, i) => s + (+i.quantity * +i.unitPrice), 0);
  const vatRate = 20;
  const vatAmount = subtotal * (vatRate / 100);
  const grandTotal = subtotal + vatAmount;

  const el = document.getElementById('qt-total');
  if (el) {
    el.innerHTML = `
      <div style="font-size:13px;color:#64748b;font-weight:500;margin-bottom:4px">Ara Toplam: ₺${fmtMoney(subtotal)}</div>
      <div style="font-size:13px;color:#64748b;font-weight:500;margin-bottom:8px">KDV (%${vatRate}): ₺${fmtMoney(vatAmount)}</div>
      <div style="font-size:16px;color:#1a56db;font-weight:700">Genel Toplam (KDV Dahil): ₺${fmtMoney(grandTotal)}</div>
    `;
  }
}

function renderQtItems() {
  const list = document.getElementById('qt-items-list');
  if (!list) return;
  if (!qtItems.length) {
    list.innerHTML = '<div style="color:#94a3b8;font-size:13px;padding:8px 0">Henüz kalem yok. "+ Kalem Ekle" butonuna tıklayın.</div>';
    updateQtTotal();
    return;
  }
  list.innerHTML = qtItems.map((item, i) => `
    <div style="display:grid;grid-template-columns:40px 2fr 100px 90px 100px 70px 30px;gap:6px;align-items:center;margin-bottom:6px">
      ${item.imageUrl 
        ? `<img src="${item.imageUrl}" style="width:36px;height:36px;border-radius:4px;object-fit:cover;border:1px solid #e2e8f0;display:block">` 
        : `<div style="width:36px;height:36px;border-radius:4px;background:#f1f5f9;border:1px solid #e2e8f0;display:flex;align-items:center;justify-content:center;color:#94a3b8;font-size:8px">YOK</div>`
      }
      <input class="form-control" placeholder="Ürün adı" value="${item.productName}"
        oninput="qtItems[${i}].productName=this.value">
      <input class="form-control" placeholder="Barkod, Kalıp, vs." value="${item.barcode || (item.moldCode ? item.moldCode + ' ' + item.productCode : item.productCode) || ''}"
        oninput="qtItems[${i}].searchQuery=this.value" onblur="handleQtProductCodeBlur(${i})">
      <input class="form-control" type="number" min="1" value="${item.quantity}"
        oninput="qtItems[${i}].quantity=this.value; updateQtTotal()">
      <input class="form-control" type="number" step="0.01" min="0" value="${item.unitPrice}"
        oninput="qtItems[${i}].manualPrice=true; qtItems[${i}].unitPrice=this.value; updateQtTotal()">
      <button type="button" class="btn btn-ghost btn-sm" onclick="openProductPicker(${i})" style="padding:5px;font-size:11px">🔍 Bul</button>
      <button type="button" class="btn btn-danger btn-sm" onclick="removeQtItem(${i})" style="padding:5px 8px">✕</button>
    </div>`).join('');
  updateQtTotal();
}

async function deleteQt(id) {
  if (!confirm('Bu teklifi silmek istiyor musunuz?')) return;
  try {
    await api(`/quotations/${id}`, { method: 'DELETE' });
    toast('Teklif silindi');
    await loadQuotations();
  } catch (e) {
    toast('Teklif silinemedi: ' + e.message, 'danger');
  }
}

// --- Ürün Arama ve Ekleme Fonksiyonları ---

async function handleQtProductCodeBlur(index) {
  const query = qtItems[index].searchQuery?.trim();
  if (!query) return;
  const dealerId = document.getElementById('qt-dealer-id')?.value;
  if (!dealerId) {
    toast('Fiyatları getirmek için önce Bayi seçmelisiniz', 'warning');
    return;
  }
  
  let moldCode = '';
  let prodCode = '';
  let barcode = '';
  
  // heuristic parse:
  // if query is all numbers and length > 8, assume barcode
  if (/^\d{8,}$/.test(query)) {
     barcode = query;
  } else if (query.includes(' ')) {
     // assume dual code: <mold> <code>
     const parts = query.split(' ');
     moldCode = parts[0];
     prodCode = parts.slice(1).join(' ');
  } else {
     // default single code
     prodCode = query;
  }

  try {
    const qs = new URLSearchParams();
    if (barcode) qs.append('barcode', barcode);
    if (moldCode) qs.append('moldCode', moldCode);
    if (prodCode) qs.append('code', prodCode);

    const p = await api(`/products/by-code?${qs.toString()}`);
    applyProductToRow(p, index, dealerId);
  } catch (err) {
    if (err.message.includes('404')) {} // Sessiz kal veya uyar
  }
}

function applyProductToRow(product, index, dealerId) {
  qtItems[index].productName = product.productName;
  qtItems[index].productCode = product.productCode;
  qtItems[index].moldCode = product.moldCode;
  qtItems[index].barcode = product.barcode;
  qtItems[index].basePrices = {
      purchasePrice: product.purchasePrice || 0,
      salePrice: product.salePrice || 0,
      listPrice: product.listPrice || 0
  };
  qtItems[index].manualPrice = false;
  qtItems[index].imageUrl = product.imageUrl;
  
  recalcRowPrice(index);
  renderQtItems();
  toast('Ürün bilgileri getirildi', 'success');
}

window.applyGlobalPriceChange = function() {
    qtItems.forEach((item, idx) => {
        if (!item.manualPrice) recalcRowPrice(idx);
    });
    renderQtItems();
}

function recalcRowPrice(index) {
    const listType = document.getElementById('qt-price-list')?.value || 'SalePrice';
    const rawPct = parseFloat(document.getElementById('qt-price-adj')?.value || 0);
    
    let basePrice = 0;
    if (listType === 'PurchasePrice') basePrice = qtItems[index].basePrices.purchasePrice;
    else if (listType === 'SalePrice') basePrice = qtItems[index].basePrices.salePrice;
    else if (listType === 'ListPrice') basePrice = qtItems[index].basePrices.listPrice;
    
    const adjustment = basePrice * (rawPct / 100);
    qtItems[index].unitPrice = (basePrice + adjustment).toFixed(2);
}

let pickerCurrentIndex = -1;
let pickerTimeout = null;

function openProductPicker(index) {
  const dealerId = document.getElementById('qt-dealer-id')?.value;
  if (!dealerId) {
    toast('Lütfen önce Bayi seçin', 'warning');
    return;
  }
  pickerCurrentIndex = index;

  const html = `
    <div id="product-picker-overlay" style="position:fixed;top:0;left:0;right:0;bottom:0;background:rgba(0,0,0,0.5);z-index:9999;display:flex;align-items:center;justify-content:center">
      <div class="card" style="width:90%;max-width:850px;max-height:90vh;display:flex;flex-direction:column;background:white">
        <div class="card-header" style="display:flex;justify-content:space-between;align-items:center">
          <h3 style="margin:0">Ürün Seç (Hızlı Arama)</h3>
          <button class="btn btn-ghost btn-sm" onclick="closeProductPicker()">Kapat</button>
        </div>
        <div class="card-body" style="overflow:hidden;display:flex;flex-direction:column;padding:16px">
          <input type="text" class="form-control" placeholder="Ürün Adı, Kodu, Marka veya Katalog Ara (Örn: coca 330)..." 
            onkeyup="handlePickerSearch(event)" id="picker-search-input" style="font-size:16px;padding:12px;margin-bottom:16px">
          
          <div style="flex:1;overflow-y:auto">
            <table style="width:100%;text-align:left;border-collapse:collapse">
              <thead><tr>
                <th style="padding:8px;border-bottom:1px solid #e2e8f0;width:40px"></th>
                <th style="padding:8px;border-bottom:1px solid #e2e8f0">Kod</th>
                <th style="padding:8px;border-bottom:1px solid #e2e8f0">Ürün Adı</th>
                <th style="padding:8px;border-bottom:1px solid #e2e8f0">Marka</th>
                <th style="padding:8px;border-bottom:1px solid #e2e8f0">Katalog</th>
                <th style="padding:8px;border-bottom:1px solid #e2e8f0">Koli Fiyat</th>
                <th style="padding:8px;border-bottom:1px solid #e2e8f0">Birim Fiyat</th>
                <th style="padding:8px;border-bottom:1px solid #e2e8f0">Stok</th>
                <th style="padding:8px;border-bottom:1px solid #e2e8f0"></th>
              </tr></thead>
              <tbody id="picker-results">
                <tr><td colspan="8" style="text-align:center;padding:20px;color:#94a3b8">Aramak için yazmaya başlayın...</td></tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  `;
  document.body.insertAdjacentHTML('beforeend', html);
  document.getElementById('picker-search-input').focus();
}

window.closeProductPicker = function() {
  const el = document.getElementById('product-picker-overlay');
  if (el) el.remove();
}

window.handlePickerSearch = function(e) {
  const q = e.target.value.trim();
  clearTimeout(pickerTimeout);
  if (!q) {
    document.getElementById('picker-results').innerHTML = '<tr><td colspan="8" style="text-align:center;padding:20px;color:#94a3b8">Aramak için yazmaya başlayın...</td></tr>';
    return;
  }
  pickerTimeout = setTimeout(() => executePickerSearch(q), 300);
}

async function executePickerSearch(q) {
  try {
    const products = await api(`/products?q=${encodeURIComponent(q)}`);
    const tbody = document.getElementById('picker-results');
    const dealerId = document.getElementById('qt-dealer-id')?.value;
    
    if (!products.length) {
      tbody.innerHTML = '<tr><td colspan="8" style="text-align:center;padding:20px;color:#94a3b8">Sonuç bulunamadı.</td></tr>';
      return;
    }

    tbody.innerHTML = products.map(p => {
      const dp = (p.dealerProducts || []).find(d => d.dealerId == dealerId);
      const casePrice = dp ? dp.casePrice : 0;
      const unitPrice = dp ? dp.unitPrice : 0;
      const stock = dp ? dp.stockQuantity : 0;
      
      const pJson = encodeURIComponent(JSON.stringify(p));
      return `
        <tr>
          <td style="padding:8px;border-bottom:1px solid #f1f5f9">
            ${p.imageUrl 
              ? `<img src="${p.imageUrl}" style="width:32px;height:32px;object-fit:cover;border-radius:4px">` 
              : `<div style="width:32px;height:32px;background:#f1f5f9;border-radius:4px;display:flex;align-items:center;justify-content:center;color:#94a3b8;font-size:8px">YOK</div>`}
          </td>
          <td style="padding:8px;border-bottom:1px solid #f1f5f9">${p.barcode || (p.moldCode ? p.moldCode + ' + ' + p.productCode : p.productCode) || '—'}</td>
          <td style="padding:8px;border-bottom:1px solid #f1f5f9"><strong>${p.productName}</strong></td>
          <td style="padding:8px;border-bottom:1px solid #f1f5f9">${p.catalog?.brand?.name || '—'}</td>
          <td style="padding:8px;border-bottom:1px solid #f1f5f9">${p.catalog?.catalogName || '—'}</td>
          <td style="padding:8px;border-bottom:1px solid #f1f5f9">₺${fmtMoney(casePrice)}</td>
          <td style="padding:8px;border-bottom:1px solid #f1f5f9">₺${fmtMoney(unitPrice)}</td>
          <td style="padding:8px;border-bottom:1px solid #f1f5f9">${stock}</td>
          <td style="padding:8px;border-bottom:1px solid #f1f5f9">
            <button type="button" class="btn btn-primary btn-sm" onclick="selectPickerProduct('${pJson}')">Seç</button>
          </td>
        </tr>`;
    }).join('');
  } catch (err) {
    document.getElementById('picker-results').innerHTML = `<tr><td colspan="8" style="color:red">Hata: ${err.message}</td></tr>`;
  }
}

window.selectPickerProduct = function(pJsonStr) {
  const p = JSON.parse(decodeURIComponent(pJsonStr));
  const dealerId = document.getElementById('qt-dealer-id').value;
  applyProductToRow(p, pickerCurrentIndex, dealerId);
  closeProductPicker();
}

window.convertToSalesOrder = function(id) {
  location.hash = '#sales-orders';
  setTimeout(() => {
    if (typeof openSalesOrderModal === 'function') {
      openSalesOrderModal(id);
    }
  }, 100);
}
