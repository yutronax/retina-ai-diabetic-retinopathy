"""Düzeltilmiş sunumu günceller: eski slaytlardaki hatalı metinleri düzeltir, her ödev slaydının arkasına
gerçek fotoğraf sonuç slaytları, sona da özet ve kaynaklar slaytı ekler.

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
RED = RGBColor.from_string("D9534F")
PAGE = RGBColor.from_string("F7F8F4")  # eski slaytların zemin rengi

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
    s.background.fill.solid()
    s.background.fill.fore_color.rgb = PAGE
    bar =s.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, 0, Emu(171450), H)
    bar.fill.solid(); bar.fill.fore_color.rgb = TEAL; bar.line.fill.background()
    text(s, 628650, 300000, 9906000, 590550, title, 25.5, True, NAVY)
    text(s, 647700, 860000, 9906000, 361950, subtitle, 12.75, False, MUTED)
    text(s, 11049000, 360000, 666750, 400050, number, 13.5, True, TEAL, PP_ALIGN.RIGHT)
    if source:
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


def note_box(slide, x, y, w, h, title, body, size=12):
    box = slide.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE, Emu(x), Emu(y), Emu(w), Emu(h))
    box.fill.solid(); box.fill.fore_color.rgb = SOFT; box.line.fill.background()
    box.adjustments[0] = 0.06
    text(slide, x + 180000, y + 120000, w - 360000, 380000, title, 14, True, NAVY)
    text(slide, x + 180000, y + 520000, w - 360000, h - 600000, body, size, False, BODY)


TOP, BOTTOM = 1400000, 6858000 - 560000
AREA_H = BOTTOM - TOP
new_slides = {}  # mevcut slayt indeksi -> arkasına eklenecek yeni slaytlar

nut = next(x for x in res["nuts"] if x["file"].startswith("nut1"))
lake = res["lake"]
lake_err = abs(lake["areaKm2"] - lake["referenceAreaKm2"]) / lake["referenceAreaKm2"] * 100
p5 = next(r for r in res["noise"]["psnr"] if r["image"] == "4.2.07.tiff" and abs(r["density"] - 0.05) < 1e-9)
p20 = next(r for r in res["noise"]["psnr"] if r["image"] == "4.2.07.tiff" and abs(r["density"] - 0.20) < 1e-9)
thr = sorted(res["threshold"], key=lambda r: -r["score"])
rgb_best = next(r for r in thr if r["channel"].startswith("RGB"))
coins_wrong = sum(1 for c in res["coins"] if abs(c["total"] - coin_gt[c["file"]]["total"]) > 0.01)
nuts_separated = nut["hazelnut"] + nut["walnut"]


# ==== 1) Mevcut slaytlardaki hatalı / eskimiş metinleri düzelt ================================
def fix(index, pairs):
    slide = prs.slides[index]
    paragraphs = []
    for sh in slide.shapes:
        if sh.has_text_frame:
            paragraphs += sh.text_frame.paragraphs
        if sh.has_table:
            for row in sh.table.rows:
                for cell in row.cells:
                    paragraphs += cell.text_frame.paragraphs
    for old, new in pairs:
        hits = [pa for pa in paragraphs if "".join(r.text for r in pa.runs) == old]
        assert len(hits) == 1, f"slayt {index + 1}: '{old}' bulunamadı ({len(hits)})"
        hits[0].runs[0].text = new


fix(2, [("Beyaz zeminde kontur ve boyut tabanlı ayrım", "Beyaz zeminde bölgeleri renklendirip piksel sayısıyla ayrım (PDF yöntemi)"),
        ("Gri tona çevir", "Gri + HSV-S maskesi"), ("Otsu eşikleme", "Otsu + delik doldurma"),
        ("Morfolojik temizlik", "Değen nesneleri ayır"), ("Konturları ölç", "Bölgeleri renklendir"),
        ("Boyuta göre ayır", "Piksel sayısıyla ayır"),
        ("5 gerçek fotoğraf", "Hedef: kendi çektiğimiz 5 fotoğraf"),
        ("Toplam doğruluğu", "Şu an: 1 açık kaynak fotoğraf"),
        ("|bulunan − gerçek| ile ölç", "Sonuç: slayt 03b")])
fix(3, [("Başlangıç dikdörtgeni", "Dikdörtgen + duvar rengi"),
        ("Sert gölgeler, kişinin bir parçası gibi seçilebilir.", "Koyu kıyafet, koyu arka plan rengine yakınsa kaybolabilir."),
        ("Aynı fotoğrafı", "Aynı kişi"), ("açık ve karmaşık iki", "açık ve karmaşık iki arka planda:"),
        ("arka planda karşılaştır", "slayt 04b")])
fix(4, [("Kenarları bulanıklaştırır", "İnce ayrıntıyı siler, şekilleri kalınlaştırır"),
        ("Daha az yumuşatma", "Ayrıntıyı daha az bozar"),
        ("Yüksek yoğunlukta ayrıntı kaybeder", f"Morfolojik değil; %20 gürültüde bile en iyi ({tr(p20['median'], 1)} dB)"),
        ("Aynı temiz görüntü ve aynı gürültü maskesi kullanılmadan PSNR karşılaştırması adil olmaz.",
         "Her yöntem aynı temiz görüntü ve aynı gürültü maskesiyle (sabit tohum) denendi; menü kullanımı: slayt 05d.")])
fix(5, [("Çıktı: her paranın değeri, yüzü ve toplam tutar",
         "Yazı-tura gerçek veride denenmedi: referans yüz görüntüsü yok; PDF'in kenar-histogram önerisi uygulanmadı.")])
fix(6, [("Su maskesi", "Eşikle + bölgeleri boya"), ("En uygun kontur", "Merkeze en yakın bölge"),
        ("Piksel alanı ve çevre", "Piksel say + genişlet−çıkar"),
        ("Alan = piksel alanı × (metre / piksel)²", "Alan = aynı renkli piksel sayısı × (metre / piksel)²"),
        ("Çevre = piksel çevresi × metre / piksel", "Çevre = (genişletilmiş − orijinal) kenar pikseli × metre / piksel")])
fix(7, [("Tek sabit eşik yerine 14 kanal-kutup adayı puanlanır",
         f"Tek sabit eşik yerine {len(thr)} aday puanlanır: 14 tek kanal + 2 RGB üçlü eşik"),
        ("Normal ve ters maske", "Normal/ters + RGB üçlü"),
        ("Histogramdaki sınıflar arası varyansı artıran global eşiği seçer.",
         "Sınıflar arası varyansı en büyük yapan global eşiği seçer.")])
fix(8, [("Deney planı ve ölçümler", "Ölçümler ve sonuçlar"),
        ("Görsel izlenim yerine her görev için ayrı ölçüt", "Her görev için ölçüt ve gerçek veride elde edilen sonuç"),
        ("Kaydedilecek sonuç", "Sonuç"),
        ("Gerçek ve bulunan adet", f"6 nesneden {nuts_separated}'i ayrıldı (1 fotoğraf)"),
        ("IoU ve sınır hatası", "Görsel kontrol (IoU için etiketli maske yok)"),
        ("Başarılı ve başarısız örnek", "2 portre × 2 arka plan; ceket kısmen kayıp"),
        ("Üç yöntemin değeri", f"%5'te medyan {tr(p5['median'], 1)} > artı {tr(p5['cross5'], 1)} > kare {tr(p5['square9'], 1)} dB"),
        ("Gerçek ve bulunan tutar", f"{coins_wrong}/7 fotoğrafta yanlış toplam"),
        ("Ölçek duyarlılığı", "Resmî alana göre hata"),
        ("m², km² ve metre", f"{tr(lake['areaKm2'])} km² (resmî 3.755, hata %{tr(lake_err, 1)})"),
        ("IoU ve seçim kararlılığı", "Geometri puanı"),
        ("Kanal, eşik ve puan", f"{thr[0]['channel']} (ters) ≈ Gri ≈ RGB üçlü"),
        ("En az bir başarısız örnek sunumda gösterilecek ve hata nedeni açıklanacak.",
         "Başarısız örnekler (Ödev 1 ve 4) nedenleriyle birlikte sonuç slaytlarında gösterildi.")])
fix(9, [("Boyut tabanlı sınıflandırma perspektiften etkilenir.", "Birbirine değen nesneler ayrılamıyor (Ödev 1 ve 4)."),
        ("Para yüzü parlama ve dönmeye duyarlıdır.", "Desenli veya gri zeminde Otsu nesneyi zeminden ayıramıyor."),
        ("Teslim ölçütü: çalışan uygulama, gerçek fotoğraflar, sayısal sonuçlar ve açıklanmış hata örnekleri",
         "Eksik: Ödev 1 için kendi çektiğimiz 5 fotoğraf ve beyaz zeminde ayrık para fotoğrafları")])

# ==== 2) Sonuç slaytları =====================================================================
# ---- Ödev 1 ----
s = frame("Ödev 1: Gerçek fotoğrafta sonuç", "Beyaz zemin, 2 ceviz + 4 fındık; nesneler birbirine geniş yüzeyle değiyor",
          "03b", "Wikimedia Commons – Two walnuts and four hazelnuts (kamu malı)")
half = AREA_H // 2 - 40000
picture(s, R / "nuts_nut1_two_walnuts_four_hazelnuts_before.jpg", 650000, TOP, 3500000, half, "Önce: girdi")
picture(s, R / "nuts_nut1_two_walnuts_four_hazelnuts_mask.png", 4250000, TOP, 3500000, half, "Ayrılmış bölgeler maskesi")
picture(s, R / "nuts_nut1_two_walnuts_four_hazelnuts_after.jpg", 650000, TOP + half + 80000, 3500000, half,
        f"Sonra: {nut['hazelnut']} fındık ayrıldı, {nut['unseparated']} birleşik leke")
picture(s, G / "nuts_count.png", 4250000, TOP + half + 80000, 3500000, half)
px = sorted(nut["pixels"])
note_box(s, 7950000, TOP, 3700000, AREA_H - 100000, "Ne öğrendik?",
         "• İlk sürüm hiçbir nesne saymadı: 5 nesne tek lekeye dönüştü ve yuvarlaklık filtresi bu lekeyi eledi.\n"
         "• Doygunluk maskesi ve delik doldurma ile maske nesneleri tam kapsıyor.\n"
         f"• Ayrılabilen fındık {tr(px[0])} piksel, birleşik leke {tr(px[-1])} piksel. Leke artık yanlışlıkla "
         "ceviz sayılmıyor, \"ayrılamadı\" olarak raporlanıyor.\n"
         "• Koşul şart: nesneler arasında boşluk. Teslim için kendi çektiğimiz 5 fotoğraf gerekli.")
new_slides[2] = [s]

# ---- Ödev 2 ----
s = frame("Ödev 2: Önce / sonra", "Düz duvar önündeki portre → açık (düz) ve karmaşık (uydu) arka plan",
          "04b", "Wikimedia Commons – resmî portreler (kamu malı, CC0); uydu: Copernicus Sentinel-2 / P. Markuse (CC BY 2.0)")
cw = 2050000
for i, stem in enumerate(["p1_higgins", "p2_schinazi"]):
    y = TOP if i == 0 else TOP + AREA_H // 2
    hh = AREA_H // 2 - 50000
    for j, (f, cap) in enumerate([(f"fg_{stem}_before.jpg", "Önce"), (f"fg_{stem}_mask.png", "Maske"),
                                  (f"fg_{stem}_bg_acik_gokyuzu_after.jpg", "Açık arka plan"),
                                  (f"fg_{stem}_bg_landscape_after.jpg", "Karmaşık arka plan")]):
        picture(s, R / f, 650000 + j * (cw + 70000), y, cw, hh, cap if i == 0 else None)
note_box(s, 9200000, TOP, 2450000, AREA_H - 100000, "Gözlem",
         "• Yalnız GrabCut saçın çevresinde beyaz duvar parçaları bırakıyordu.\n"
         "• Duvar rengi üst/yan kenarlardan öğrenilip kesin arka plan yapıldı.\n"
         "• Maske arka plandan bağımsız; açık arka planda kenar hataları daha az göze batıyor.\n"
         "• Koyu ceketin bir kısmı kayıp; nedeni doğrulanmadı.", 11)
new_slides[3] = [s]

# ---- Ödev 3 ----
s = frame("Ödev 3: Tuz-biber sonuçları", "%5 tuz-biber gürültüsü eklenmiş Peppers; aynı gürültü maskesi her yönteme",
          "05b", "USC-SIPI Image Database – Misc (eğitim/araştırma amaçlı)")
q = 1750000
for i, (f, cap) in enumerate([("noisy", "Gürültülü"), ("square9", "9'lu kare"), ("cross5", "5'li artı"), ("median", "Medyan")]):
    picture(s, R / f"noise_4_2_07_{f}.png", 650000 + (i % 2) * (q + 80000), TOP + (i // 2) * (q + 260000), q, q + 200000, cap)
picture(s, G / "noise_psnr.png", 4400000, TOP, 4300000, 3100000)
note_box(s, 8850000, TOP, 2800000, AREA_H - 100000, "Sonuç",
         f"%5 gürültüde PSNR:\n• Gürültülü: {tr(p5['noisy'], 1)} dB\n• 9'lu kare: {tr(p5['square9'], 1)} dB\n"
         f"• 5'li artı: {tr(p5['cross5'], 1)} dB\n• Medyan: {tr(p5['median'], 1)} dB\n\n"
         "Artı şablon kareden iyi: daha az komşu kullandığı için ayrıntıyı daha az siler. "
         "Medyan her yoğunlukta en iyisi.")
text(s, 4400000, TOP + 3200000, 4300000, 1200000,
     f"Gürültü %5'ten %20'ye çıkınca kare {tr(p5['square9'] - p20['square9'], 1)} dB, medyan yalnız "
     f"{tr(p5['median'] - p20['median'], 1)} dB kaybediyor. Sıralama House ve Aerial'da da aynı.", 12, False, BODY)

s2 = frame("Ödev 3: Kenar açıklıklarını kapatma", "Canny kenarlarındaki kopukluklar kapama (genişlet + aşındır) ile birleştirildi",
           "05c", "USC-SIPI Image Database – House")
for i, (f, cap) in enumerate([("canny", "Canny kenarları"), ("square9", "9'lu kare kapama"), ("cross5", "5'li artı kapama")]):
    picture(s2, R / f"edges_house_{f}.png", 650000 + i * 2350000, TOP, 2250000, 2600000, cap)
picture(s2, G / "edges_segments.png", 650000, TOP + 2650000, 4200000, AREA_H - 2650000)
e = next(x for x in res["noise"]["edges"] if x["image"] == "house.tiff")
note_box(s2, 7850000, TOP, 3800000, AREA_H - 100000, "Hangisi daha iyi kapattı?",
         f"Kopuk parça sayısı {e['segmentsBefore']} → kare {e['segmentsSquare9']}, artı {e['segmentsCross5']}.\n\n"
         "• Kare, çapraz boşlukları da kapattığı için daha çok birleştiriyor.\n"
         "• Ama az parça her zaman daha iyi değil: kare çatı ve ağaç dokusunu büyük lekelere dönüştürüyor "
         "(aşırı kapama).\n"
         "• Artı yalnız yatay/dikey boşlukları kapatıyor, çizgileri daha ince ve okunur bırakıyor.\n"
         "→ Kopuklukları kapatmada kare, kenarın biçimini korumada artı daha iyi.")

s3 = frame("Ödev 3: Menüden seçim ve renk aralığı",
           "Uygulamanın kendi ekranı: şablon, işlem, tur ve R/G/B aralığını kullanıcı seçer",
           "05d", "Uygulama ekran görüntüsü; görüntü: USC-SIPI Peppers")
picture(s3, R / "noise_ui_menu.png", 650000, TOP, 7600000, AREA_H - 100000)
ui = res["noiseUi"]
note_box(s3, 8450000, TOP, 3200000, AREA_H - 100000, "Bu örnekte",
         f"• Şablon: {ui['kernel']}\n• İşlem: {ui['operation']}, {ui['iterations']} tur\n"
         f"• R {ui['r']}, G {ui['g']}, B {ui['b']}\n\n"
         "Yalnız bu aralıktaki (kırmızı) pikseller komşu sayılır; ortaya çıkan maske seçilen şablonla genişletilir.\n\n"
         "Menüdeki seçenekler: 9'lu kare / 5'li artı; aşındır, genişlet, açma, kapama; 1-10 tur; her kanal için min-maks.")
new_slides[4] = [s, s2, s3]

# ---- Ödev 4 ----
s = frame("Ödev 4: Gerçek veri testi", "Roboflow Bozuk Paralar setinden yalnız 25 kr, 50 kr ve 1 TL içeren 7 fotoğraf",
          "06b", "Roboflow Universe – Bozuk Paralar v2, abdulkadir-caglar (CC BY 4.0)")
picture(s, R / "coins_coin3_before.jpg", 650000, TOP, 2500000, 2700000, "Önce: girdi")
picture(s, R / "coins_coin3_mask.png", 3250000, TOP, 2500000, 2700000, "Otsu maskesi")
picture(s, G / "coins_total.png", 650000, TOP + 2750000, 5100000, AREA_H - 2750000)
found = sum(1 for c in res["coins"] if c["total"] > 0)
note_box(s, 6000000, TOP, 5650000, AREA_H - 100000, "Neden başarısız?",
         f"{coins_wrong}/7 fotoğrafta toplam yanlış ({found} fotoğrafta yalnızca 1 para algılandı).\n\n"
         "• Fotoğraflar gri tonlu ve puantiyeli kumaş üzerinde: Otsu eşiği paraları zeminden ayıramıyor.\n"
         "• Paralar üst üste ve birbirine değiyor; birleşik lekeler yuvarlaklık filtresine takılıyor.\n"
         "• Ölçek \"en büyük para 1 TL\" varsayımına dayanıyor; tek para bulununca hep 1 TL çıkıyor.\n"
         "• Yazı-tura: referans yüz görüntüsü olmadığı için hiç test edilmedi.\n\n"
         "Algoritma ödevin tarif ettiği koşul için tasarlandı (beyaz kâğıt, ayrık paralar); o koşulda çekilmiş "
         "fotoğrafla tekrar denenmeli.")
new_slides[5] = [s]


# ---- Ödev 5 ----
s = frame("Ödev 5: Van Gölü ölçümü", "Landsat görüntüsü; ölçek çizgisi Nemrut kalderası – Süphan Dağı (~60 km)",
          "07b", "NASA / Landsat – Lakevanlandsat.jpeg, Wikimedia Commons (kamu malı)")
third = 2600000
qh = AREA_H // 2 - 40000
picture(s, R / "lake_before.jpg", 650000, TOP, third, qh, "Önce: uydu görüntüsü")
picture(s, R / "lake_regions.png", 650000 + third + 80000, TOP, third, qh, f"{lake['regionCount']} bölge, her biri ayrı renk")
picture(s, R / "lake_after.jpg", 650000, TOP + qh + 80000, third, qh, "Sonra: kenar, merkez (+), göl merkezi")
picture(s, G / "lake_area.png", 650000 + third + 80000, TOP + qh + 80000, third, qh)
note_box(s, 6200000, TOP, 5450000, AREA_H - 100000, "Sonuç",
         f"• Eşik: {lake['method']}, {lake['threshold']:.0f}\n"
         f"• Göl seçimi (PDF): ağırlık merkezi resmin ortasına en yakın bölge ({lake['centerDistancePixels']:.0f} piksel uzakta)\n"
         f"• Ölçek: {lake['scalePixels']:.0f} piksel = 60 km → {60000 / lake['scalePixels']:.0f} m/piksel\n"
         f"• Alan (aynı renkli piksel sayısı): {tr(lake['areaKm2'])} km² — resmî 3.755 km², hata %{tr(lake_err, 1)}\n"
         f"• Çevre (genişlet − orijinal kenar pikselleri): {tr(lake['perimeterKm'])} km\n"
         f"• Karşılaştırma, kontur uzunluğu: {tr(lake['contourPerimeterKm'])} km\n\n"
         "Kenar pikseli sayımı çapraz adımları 1 piksel saydığı için çevreyi kontur uzunluğundan kısa buluyor. "
         "60 km'lik ölçek iki noktanın koordinatlarından hesaplandı; noktalar görüntüde göz kararı seçildi.", 12)
new_slides[6] = [s]

# ---- Araştırma ----
s = frame("Araştırma: Eşik adaylarının karşılaştırması",
          f"Aynı uydu görüntüsünde {len(thr)} aday: 7 kanal × normal/ters + 2 RGB üçlü eşik",
          "08b", "NASA / Landsat – Lakevanlandsat.jpeg (kamu malı)")
picture(s, G / "threshold_scores.png", 650000, TOP, 5200000, AREA_H - 100000)
picture(s, R / "threshold_best_mask.png", 6050000, TOP, 2700000, 2300000, "En yüksek puanlı aday")
picture(s, R / "threshold_worst_mask.png", 8950000, TOP, 2700000, 2300000, "En düşük puanlı aday (bölge yok)")
note_box(s, 6050000, TOP + 2450000, 5600000, AREA_H - 2550000, "Bulgu",
         f"• Kazanan: {thr[0]['channel']} (ters), eşik {thr[0]['threshold']:.0f}. Gri, HSV-V ve RGB üçlü eşik "
         f"({tr(rgb_best['score'], 2)} puan) neredeyse eşit: koyu su her kanalda ayrışıyor.\n"
         "• PDF'in sorusu: üç kanala ayrı eşik bu görüntüde tek kanaldan daha iyi değil, aynı.\n"
         "• -3'teki adaylarda uygun boyutta bölge çıkmadı.\n"
         "• Deney tek görüntüde yapıldı; genelleme için farklı görüntü türleri gerekli.", 11)
new_slides[7] = [s]

# ---- Özet + kaynaklar (sona) ----
summary = frame("Özet", "Her ödev için gerçek veride durum", "11", None)
rows = [("Ödev", "Durum", "Ana sonuç"),
        ("1 Ceviz/fındık", "Eksik", f"1 fotoğraf; 6 nesneden {nuts_separated}'i ayrıldı; kendi 5 fotoğrafımız gerekli"),
        ("2 Arka plan", "Çalışıyor", "Duvar rengi düzeltmesiyle temiz maske; ceket kısmen kayıp"),
        ("3 Tuz-biber", "Çalışıyor", "Medyan > artı > kare; menü ve RGB aralığı uygulamada"),
        ("4 Bozuk para", "Başarısız", f"{coins_wrong}/7 yanlış; beyaz zeminde ayrık paralarla test edilmeli"),
        ("5 Van Gölü", "Çalışıyor", f"{tr(lake['areaKm2'])} km², resmî değere göre %{tr(lake_err, 1)} hata"),
        ("Araştırma", "Çalışıyor", "Tek kanal ≈ RGB üçlü eşik; tek görüntüde denendi")]
tbl = summary.shapes.add_table(len(rows), 3, Emu(650000), Emu(TOP), Emu(10950000), Emu(len(rows) * 520000)).table
tbl.columns[0].width, tbl.columns[1].width, tbl.columns[2].width = Emu(2300000), Emu(1700000), Emu(6950000)
status_color = {"Çalışıyor": TEAL, "Eksik": AMBER, "Başarısız": RED}
for r_i, row in enumerate(rows):
    for c_i, val in enumerate(row):
        cell = tbl.cell(r_i, c_i)
        cell.text = val
        run = cell.text_frame.paragraphs[0].runs[0]
        run.font.size = Pt(13)
        run.font.bold = r_i == 0 or c_i == 1
        cell.fill.solid()
        if r_i == 0:
            cell.fill.fore_color.rgb = NAVY
            run.font.color.rgb = RGBColor(255, 255, 255)
        else:
            cell.fill.fore_color.rgb = RGBColor(255, 255, 255)
            run.font.color.rgb = status_color.get(val, BODY) if c_i == 1 else BODY

refs = frame("Kaynaklar", "Kullanılan veri setleri ve görüntüler", "12", None)
text(refs, 650000, TOP, 10950000, AREA_H,
     "• Ödev tanımları: Görüntü İşleme 8. Hafta ders notu (PDF)\n"
     "• Ceviz/fındık: Wikimedia Commons, \"Two walnuts and four hazelnuts\" (kamu malı)\n"
     "• Portreler: Wikimedia Commons, resmî portreler (kamu malı, CC0)\n"
     "• Uydu arka planı: Copernicus Sentinel-2, işleyen P. Markuse, Wikimedia Commons (CC BY 2.0)\n"
     "• Tuz-biber ve kenar deneyleri: USC-SIPI Image Database, Miscellaneous (eğitim/araştırma amaçlı)\n"
     "• Bozuk paralar: Roboflow Universe, Bozuk Paralar v2, abdulkadir-caglar (CC BY 4.0)\n"
     "• Para çapları: TCMB madeni para bilgileri\n"
     "• Van Gölü: NASA/Landsat, \"Lakevanlandsat.jpeg\", Wikimedia Commons (kamu malı); resmî alan 3.755 km²\n"
     "• Yazılım: OpenCvSharp5, .NET 8 Windows Forms; grafikler matplotlib", 15, False, BODY)

# ---- Yeni slaytları ilgili ödev slaytının arkasına taşı; özet ve kaynaklar en sona ----
sld_ids = prs.slides._sldIdLst
all_ids = list(sld_ids)
n_new = sum(len(v) for v in new_slides.values()) + 2
original, added = all_ids[: len(all_ids) - n_new], all_ids[len(all_ids) - n_new:]
order, k = [], 0
for i, el in enumerate(original):
    order.append(el)
    for _ in new_slides.get(i, []):
        order.append(added[k]); k += 1
order += added[k:]
for el in list(sld_ids):
    sld_ids.remove(el)
for el in order:
    sld_ids.append(el)

prs.save(DST)
print("Kaydedildi:", DST.name, "slayt sayısı:", len(prs.slides))
