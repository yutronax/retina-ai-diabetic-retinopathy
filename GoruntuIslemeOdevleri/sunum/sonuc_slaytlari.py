"""Düzeltilmiş sunuma, her ödev slaydının hemen arkasına gerçek fotoğraf sonuç slaytları ekler.

Kullanım: python sonuc_slaytlari.py   (sunum/ klasöründen; önce grafikler.py çalıştırılmalı)
"""
import json
from pathlib import Path

from PIL import Image
from pptx import Presentation
from pptx.dml.color import RGBColor
from pptx.enum.shapes import MSO_SHAPE
from pptx.enum.text import PP_ALIGN
from pptx.util import Emu, Pt

ROOT = Path(__file__).parent
SRC = ROOT / "Goruntu_Isleme_8_Hafta_Sunumu_duzeltilmis.pptx"
DST = ROOT / "Goruntu_Isleme_8_Hafta_Sunumu_sonuclu.pptx"
R = ROOT / "sonuclar"
G = ROOT / "grafikler"
res = json.loads((R / "results.json").read_text(encoding="utf-8"))
coin_gt = json.loads((ROOT / "../data/real/coins/ground_truth.json").read_text(encoding="utf-8"))

TEAL, NAVY, MUTED, BODY, AMBER = (RGBColor.from_string(x) for x in ("0BA6B4", "071A2B", "5F6F7B", "142534", "F2A900"))
SOFT = RGBColor.from_string("EAF7F8")

prs = Presentation(SRC)
W, H = prs.slide_width, prs.slide_height
BLANK = prs.slide_layouts[5]


def tr(v, d=0):
    return f"{v:,.{d}f}".replace(",", "X").replace(".", ",").replace("X", ".")


def text(slide, x, y, w, h, s, size=13.5, bold=False, color=BODY, align=PP_ALIGN.LEFT):
    tb = slide.shapes.add_textbox(Emu(x), Emu(y), Emu(w), Emu(h))
    tf = tb.text_frame
    tf.word_wrap = True
    for i, line in enumerate(s.split("\n")):
        p = tf.paragraphs[0] if i == 0 else tf.add_paragraph()
        p.alignment = align
        r = p.add_run()
        r.text = line
        r.font.size, r.font.bold, r.font.color.rgb = Pt(size), bold, color
    return tb


def frame(title, subtitle, number, source):
    s = prs.slides.add_slide(BLANK)
    bar = s.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, 0, Emu(171450), H)
    bar.fill.solid(); bar.fill.fore_color.rgb = TEAL; bar.line.fill.background()
    text(s, 628650, 300000, 9906000, 590550, title, 25.5, True, NAVY)
    text(s, 647700, 860000, 9906000, 361950, subtitle, 12.75, False, MUTED)
    text(s, 11049000, 360000, 666750, 400050, number, 13.5, True, TEAL, PP_ALIGN.RIGHT)
    text(s, 647700, H - 380000, 10900000, 280000, "Kaynak: " + source, 9, False, MUTED)
    return s


def picture(slide, path, x, y, max_w, max_h, caption=None):
    """Oranı koruyarak kutuya sığdırır ve ortalar; altına küçük başlık yazar."""
    with Image.open(path) as im:
        iw, ih = im.size
    cap_h = 260000 if caption else 0
    scale = min(max_w / iw, (max_h - cap_h) / ih)
    w, h = int(iw * scale), int(ih * scale)
    px, py = x + (max_w - w) // 2, y + (max_h - cap_h - h) // 2
    slide.shapes.add_picture(str(path), Emu(px), Emu(py), Emu(w), Emu(h))
    if caption:
        text(slide, x, py + h + 30000, max_w, 250000, caption, 11, True, NAVY, PP_ALIGN.CENTER)


def note_box(slide, x, y, w, h, title, body):
    box = slide.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE, Emu(x), Emu(y), Emu(w), Emu(h))
    box.fill.solid(); box.fill.fore_color.rgb = SOFT; box.line.fill.background()
    box.adjustments[0] = 0.06
    text(slide, x + 180000, y + 120000, w - 360000, 380000, title, 14, True, NAVY)
    text(slide, x + 180000, y + 520000, w - 360000, h - 600000, body, 12, False, BODY)


TOP, BOTTOM = 1400000, 6858000 - 560000
AREA_H = BOTTOM - TOP
new_slides = {}  # mevcut slayt indeksi -> arkasına eklenecek yeni slaytlar

# ---- Ödev 1 -------------------------------------------------------------------------------
nut = next(x for x in res["nuts"] if x["file"].startswith("nut1"))
s = frame("Ödev 1 · Gerçek fotoğrafta sonuç", "Beyaz zemin, 2 ceviz + 4 fındık; nesneler birbirine değiyor",
          "03b", "Wikimedia Commons – Two walnuts and four hazelnuts (kamu malı)")
half = AREA_H // 2 - 40000
picture(s, R / "nuts_nut1_two_walnuts_four_hazelnuts_before.jpg", 650000, TOP, 3500000, half, "Önce: girdi")
picture(s, R / "nuts_nut1_two_walnuts_four_hazelnuts_mask.png", 4250000, TOP, 3500000, half, "Maske (doygunluk + delik doldurma)")
picture(s, R / "nuts_nut1_two_walnuts_four_hazelnuts_after.jpg", 650000, TOP + half + 80000, 3500000, half,
        f"Sonra: {nut['hazelnut']} fındık, {nut['walnut']} ceviz bulundu")
picture(s, G / "nuts_count.png", 4250000, TOP + half + 80000, 3500000, half)
note_box(s, 7950000, TOP, 3700000, AREA_H - 100000, "Ne öğrendik?",
         "• İlk sürüm hiçbir nesne bulamadı: parlak fındık yüzeyleri zeminle karıştı.\n"
         "• Doygunluk (HSV-S) maskesi ve delik doldurma eklendi; maske artık nesneleri tam kapsıyor.\n"
         "• Birbirine geniş yüzeyle değen 5 nesne tek leke kalıyor; mesafe dönüşümüyle ayırma yalnız "
         "sağdaki fındığı ayırabildi.\n"
         "• Çekim koşulu şart: nesneler arasında boşluk. Teslim için kendi çektiğimiz 5 fotoğraf gerekli.")
new_slides[2] = [s]

# ---- Ödev 2 -------------------------------------------------------------------------------
s = frame("Ödev 2 · Önce / sonra", "Düz duvar önündeki portre → Van Gölü uydu görüntüsü önüne",
          "04b", "Wikimedia Commons – resmî portreler (kamu malı, CC0); arka plan: Copernicus Sentinel-2 / P. Markuse (CC BY 2.0)")
cw = 2600000
for i, stem in enumerate(["p1_higgins", "p2_schinazi"]):
    y = TOP if i == 0 else TOP + AREA_H // 2
    picture(s, R / f"fg_{stem}_before.jpg", 650000, y, cw, AREA_H // 2 - 50000, "Önce" if i == 0 else None)
    picture(s, R / f"fg_{stem}_mask.png", 650000 + cw + 100000, y, cw, AREA_H // 2 - 50000, "Maske" if i == 0 else None)
    picture(s, R / f"fg_{stem}_after.jpg", 650000 + 2 * (cw + 100000), y, cw, AREA_H // 2 - 50000, "Sonra" if i == 0 else None)
note_box(s, 9050000, TOP, 2600000, AREA_H - 100000, "Düzeltme",
         "• Yalnız GrabCut, saçın çevresinde büyük beyaz duvar parçaları bıraktı.\n"
         "• Duvar rengi üst ve yan kenar şeritlerinden öğrenildi; ona benzeyen ve kenara bağlı pikseller "
         "kesin arka plan yapıldı.\n"
         "• Kalan sorun: koyu ceket koyu gölgeyle karışıyor, gövde kısmen kayboluyor.")
new_slides[3] = [s]

# ---- Ödev 3 -------------------------------------------------------------------------------
s = frame("Ödev 3 · Tuz-biber sonuçları", "%5 tuz-biber gürültüsü eklenmiş Peppers görüntüsü; aynı gürültü her yönteme",
          "05b", "USC-SIPI Image Database – Misc (eğitim/araştırma amaçlı)")
q = 1750000
for i, (f, cap) in enumerate([("noisy", "Gürültülü"), ("square9", "9'lu kare"), ("cross5", "5'li artı"), ("median", "Medyan")]):
    picture(s, R / f"noise_4_2_07_{f}.png", 650000 + (i % 2) * (q + 80000), TOP + (i // 2) * (q + 260000), q, q + 200000, cap)
picture(s, G / "noise_psnr.png", 4400000, TOP, 4300000, 3100000)
p5 = next(r for r in res["noise"]["psnr"] if r["image"] == "4.2.07.tiff" and abs(r["density"] - 0.05) < 1e-9)
note_box(s, 8850000, TOP, 2800000, AREA_H - 100000, "Sonuç",
         f"%5 gürültüde PSNR:\n• Gürültülü: {tr(p5['noisy'],1)} dB\n• 9'lu kare: {tr(p5['square9'],1)} dB\n"
         f"• 5'li artı: {tr(p5['cross5'],1)} dB\n• Medyan: {tr(p5['median'],1)} dB\n\n"
         "Artı şablon kareden iyi: daha az komşu kullandığı için ayrıntıyı daha az siler. "
         "Medyan her yoğunlukta en iyisi.")
text(s, 4400000, TOP + 3200000, 4300000, 1200000,
     "Gürültü yoğunluğu arttıkça morfolojik filtreler hızla bozuluyor; medyan yalnızca ~4 dB kaybediyor. "
     "Aynı sıralama House ve Aerial görüntülerinde de tekrarlandı.", 12, False, BODY)
s2 = frame("Ödev 3 · Kenar açıklıklarını kapatma", "Canny kenarlarındaki kopukluklar kapama (genişlet + aşındır) ile birleştirildi",
           "05c", "USC-SIPI Image Database – House")
for i, (f, cap) in enumerate([("canny", "Canny kenarları"), ("square9", "9'lu kare kapama"), ("cross5", "5'li artı kapama")]):
    picture(s2, R / f"edges_house_{f}.png", 650000 + i * 2350000, TOP, 2250000, 2600000, cap)
picture(s2, G / "edges_segments.png", 650000, TOP + 2650000, 4200000, AREA_H - 2650000)
e = next(x for x in res["noise"]["edges"] if x["image"] == "house.tiff")
note_box(s2, 7850000, TOP, 3800000, AREA_H - 100000, "Hangisi daha iyi kapattı?",
         f"House görüntüsünde kopuk parça sayısı {e['segmentsBefore']} → kare ile {e['segmentsSquare9']}, "
         f"artı ile {e['segmentsCross5']}.\n\n• 9'lu kare açıklıkları daha çok kapatıyor; çapraz boşlukları da "
         "dolduruyor.\n• 5'li artı yalnız yatay/dikey boşlukları kapatıyor ama kenarları daha az kalınlaştırıyor.\n"
         "• Gürültü temizlemede tersi geçerliydi: artı şablon daha iyiydi.")
new_slides[4] = [s, s2]

# ---- Ödev 4 -------------------------------------------------------------------------------
s = frame("Ödev 4 · Gerçek veri testi", "Roboflow Bozuk Paralar setinden yalnız 25 kr, 50 kr ve 1 TL içeren 7 fotoğraf",
          "06b", "Roboflow Universe – Bozuk Paralar v2, abdulkadir-caglar (CC BY 4.0)")
picture(s, R / "coins_coin3_before.jpg", 650000, TOP, 2500000, 2700000, "Önce: girdi")
picture(s, R / "coins_coin3_mask.png", 3250000, TOP, 2500000, 2700000, "Otsu maskesi")
picture(s, G / "coins_total.png", 650000, TOP + 2750000, 5100000, AREA_H - 2750000)
found = sum(1 for c in res["coins"] if c["total"] > 0)
note_box(s, 6000000, TOP, 5650000, AREA_H - 100000, "Neden başarısız?",
         f"7 fotoğrafın hiçbirinde doğru toplam bulunamadı ({found} fotoğrafta yalnızca 1 para algılandı).\n\n"
         "• Fotoğraflar gri tonlu ve puantiyeli kumaş üzerinde: Otsu eşiği paraları zeminden ayıramıyor.\n"
         "• Paralar üst üste ve birbirine değiyor; dairesellik filtresi birleşik lekeleri eliyor.\n"
         "• Çap ölçeği \"en büyük para 1 TL\" varsayımına dayanıyor; tek para bulununca hep 1 TL çıkıyor.\n\n"
         "Algoritma ödevin tarif ettiği koşul için tasarlandı: beyaz kâğıt, ayrık paralar. "
         "Kendi çektiğimiz fotoğraflarla tekrar test edilecek.")
new_slides[5] = [s]

# ---- Ödev 5 -------------------------------------------------------------------------------
lake = res["lake"]
s = frame("Ödev 5 · Van Gölü ölçümü", "Landsat görüntüsü; ölçek çizgisi Nemrut kalderası – Süphan Dağı (~60 km)",
          "07b", "NASA / Landsat – Lakevanlandsat.jpeg, Wikimedia Commons (kamu malı)")
half = AREA_H // 2 - 40000
picture(s, R / "lake_before.jpg", 650000, TOP, 3550000, half, "Önce: uydu görüntüsü")
picture(s, R / "lake_after.jpg", 650000, TOP + half + 80000, 3550000, half, "Sonra: kontur, ağırlık merkezi, ölçek çizgisi")
picture(s, G / "lake_area.png", 4350000, TOP, 3600000, 3100000)
err = abs(lake["areaKm2"] - lake["referenceAreaKm2"]) / lake["referenceAreaKm2"] * 100
note_box(s, 8100000, TOP, 3550000, AREA_H - 100000, "Sonuç",
         f"• Seçilen kanal: {lake['method']}, eşik {lake['threshold']:.0f}\n"
         f"• Ölçek: {lake['scalePixels']:.0f} piksel = 60 km → {60000 / lake['scalePixels']:.0f} m/piksel\n"
         f"• Alan: {tr(lake['areaKm2'])} km² (resmî 3.755 km², hata %{tr(err,1)})\n"
         f"• Çevre: {tr(lake['perimeterKm'])} km\n\n"
         "Hatanın ana kaynağı ölçek çizgisi: uçlar göz kararı seçildi, 1 piksellik kayma alanı ~%1 değiştirir.")
new_slides[6] = [s]

# ---- Araştırma ----------------------------------------------------------------------------
best = max(res["threshold"], key=lambda r: r["score"])
s = frame("Araştırma · Eşik adaylarının karşılaştırması", "Aynı uydu görüntüsünde 7 kanal × normal/ters = 14 Otsu adayı",
          "08b", "NASA / Landsat – Lakevanlandsat.jpeg (kamu malı)")
picture(s, G / "threshold_scores.png", 650000, TOP, 5200000, AREA_H - 100000)
picture(s, R / "threshold_best_mask.png", 6050000, TOP, 2700000, 2300000, "En yüksek puanlı aday")
picture(s, R / "threshold_worst_mask.png", 8950000, TOP, 2700000, 2300000, "En düşük puanlı aday (bölge yok)")
note_box(s, 6050000, TOP + 2450000, 5600000, AREA_H - 2550000, "Bulgu",
         f"• Kazanan: {best['channel']}{' (ters)' if best['inverted'] else ''}, eşik {best['threshold']:.0f}. "
         "Gri, HSV-V ve Yeşil kanalları neredeyse eşit puan aldı: koyu su her kanalda ayrışıyor.\n"
         "• -3'teki adaylarda uygun boyutta bölge çıkmadı (maske ya neredeyse tüm görüntü ya da kırıntılar).\n"
         "• Tek kanal yerine adayları puanlamak, yanlış kutup (normal/ters) seçimini otomatik eliyor.")
new_slides[7] = [s]

# ---- Yeni slaytları ilgili ödev slaytının arkasına taşı -------------------------------------
sld_ids = prs.slides._sldIdLst
original = list(sld_ids)[: len(prs.slides) - sum(len(v) for v in new_slides.values())]
added = list(sld_ids)[len(original):]
order, k = [], 0
for i, el in enumerate(original):
    order.append(el)
    for _ in new_slides.get(i, []):
        order.append(added[k]); k += 1
for el in list(sld_ids):
    sld_ids.remove(el)
for el in order:
    sld_ids.append(el)

prs.save(DST)
print("Kaydedildi:", DST.name, "slayt sayısı:", len(prs.slides))
