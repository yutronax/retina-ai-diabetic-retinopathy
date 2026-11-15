"""C# toplu modunun ürettiği results.json'dan sunum grafiklerini üretir.

Kullanım: python grafikler.py  (sunum/ klasöründen)
Girdi : sonuclar/results.json (düzeltme sonrası), sonuclar_once/results.json (düzeltme öncesi),
        ../data/real/coins/ground_truth.json
Çıktı : grafikler/*.png
"""
import json
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt

ROOT = Path(__file__).parent
OUT = ROOT / "grafikler"
OUT.mkdir(exist_ok=True)

TEAL, AMBER, NAVY, GREY, RED = "#0BA6B4", "#F2A900", "#071A2B", "#9AA5AE", "#D9534F"
plt.rcParams.update({
    "font.family": "DejaVu Sans", "font.size": 12, "axes.spines.top": False,
    "axes.spines.right": False, "axes.edgecolor": "#5F6F7B", "axes.labelcolor": NAVY,
    "xtick.color": NAVY, "ytick.color": NAVY, "axes.titleweight": "bold", "axes.titlecolor": NAVY,
    "figure.dpi": 150, "savefig.bbox": "tight",
})

after = json.loads((ROOT / "sonuclar/results.json").read_text(encoding="utf-8"))
before = json.loads((ROOT / "sonuclar_once/results.json").read_text(encoding="utf-8"))
coin_gt = json.loads((ROOT / "../data/real/coins/ground_truth.json").read_text(encoding="utf-8"))


def label_bars(ax, bars, fmt="{:.0f}"):
    for b in bars:
        ax.annotate(fmt.format(b.get_height()), (b.get_x() + b.get_width() / 2, b.get_height()),
                    ha="center", va="bottom", fontsize=11, color=NAVY, xytext=(0, 2), textcoords="offset points")


# 1) Ceviz/fındık: gerçek vs düzeltme öncesi vs sonrası (nut1 – tek uygun açık fotoğraf)
nut_before = next(x for x in before["nuts"] if x["file"].startswith("nut1"))
nut_after = next(x for x in after["nuts"] if x["file"].startswith("nut1"))
fig, ax = plt.subplots(figsize=(6.4, 4))
cats = ["Fındık", "Ceviz", "Ayrılamadı\n(birleşik leke)"]
vals = {"Gerçek": [4, 2, 0],
        "İlk sürüm": [nut_before["hazelnut"], nut_before["walnut"], 0],
        "Son sürüm": [nut_after["hazelnut"], nut_after["walnut"], nut_after.get("unseparated", 0)]}
w = 0.26
for i, (name, color) in enumerate([("Gerçek", NAVY), ("İlk sürüm", GREY), ("Son sürüm", TEAL)]):
    label_bars(ax, ax.bar([c + (i - 1) * w for c in range(3)], vals[name], w, label=name, color=color))
ax.set_xticks(range(3), cats)
ax.set_ylabel("Adet")
ax.set_title("Ceviz/fındık sayımı: gerçek ve bulunan")
ax.legend(frameon=False)
fig.savefig(OUT / "nuts_count.png")
plt.close(fig)

# 2) Tuz-biber: PSNR – gürültü yoğunluğu (Peppers)
rows = [r for r in after["noise"]["psnr"] if r["image"] == "4.2.07.tiff"]
d = [r["density"] * 100 for r in rows]
fig, ax = plt.subplots(figsize=(6.4, 4))
for key, name, color, style in [("noisy", "Gürültülü", GREY, "--"), ("square9", "9'lu kare", AMBER, "-"),
                                ("cross5", "5'li artı", TEAL, "-"), ("median", "Medyan 3×3", NAVY, "-")]:
    ax.plot(d, [r[key] for r in rows], style, marker="o", color=color, label=name, linewidth=2)
ax.set_xlabel("Tuz-biber yoğunluğu (%)")
ax.set_ylabel("PSNR (dB) – yüksek daha iyi")
ax.set_title("Gürültü arttıkça filtrelerin başarısı (Peppers)")
ax.legend(frameon=False)
ax.grid(axis="y", alpha=0.25)
fig.savefig(OUT / "noise_psnr.png")
plt.close(fig)

# 2b) Üç görüntüde %5 gürültü PSNR
names = {"4.2.07.tiff": "Peppers", "house.tiff": "House", "5.2.09.tiff": "Aerial"}
rows5 = [r for r in after["noise"]["psnr"] if abs(r["density"] - 0.05) < 1e-9]
fig, ax = plt.subplots(figsize=(6.4, 4))
w = 0.2
for i, (key, name, color) in enumerate([("noisy", "Gürültülü", GREY), ("square9", "9'lu kare", AMBER),
                                        ("cross5", "5'li artı", TEAL), ("median", "Medyan", NAVY)]):
    label_bars(ax, ax.bar([j + (i - 1.5) * w for j in range(len(rows5))], [r[key] for r in rows5], w,
                          label=name, color=color), "{:.1f}")
ax.set_xticks(range(len(rows5)), [names[r["image"]] for r in rows5])
ax.set_ylabel("PSNR (dB)")
ax.set_ylim(0, max(r["median"] for r in rows5) * 1.18)
ax.set_title("%5 tuz-biber gürültüsünde PSNR")
ax.legend(frameon=False, ncol=4, loc="upper center", bbox_to_anchor=(0.5, -0.1))
fig.savefig(OUT / "noise_psnr_bar.png")
plt.close(fig)

# 2c) Kenar boşluğu kapatma: parça sayısı (az = daha bütünleşik kenar)
edges = after["noise"]["edges"]
fig, ax = plt.subplots(figsize=(6.4, 4))
w = 0.26
for i, (key, name, color) in enumerate([("segmentsBefore", "Canny (kapatma öncesi)", GREY),
                                        ("segmentsSquare9", "9'lu kare kapama", AMBER),
                                        ("segmentsCross5", "5'li artı kapama", TEAL)]):
    label_bars(ax, ax.bar([j + (i - 1) * w for j in range(len(edges))], [e[key] for e in edges], w,
                          label=name, color=color))
ax.set_xticks(range(len(edges)), [names[e["image"]] for e in edges])
ax.set_ylabel("Kopuk kenar parçası sayısı")
ax.set_title("Kenar açıklıklarını kapatma (az parça = daha iyi)")
ax.legend(frameon=False)
fig.savefig(OUT / "edges_segments.png")
plt.close(fig)

# 3) Bozuk para: gerçek vs bulunan toplam tutar
coins = after["coins"]
fig, ax = plt.subplots(figsize=(6.4, 4))
w = 0.38
xs = range(len(coins))
label_bars(ax, ax.bar([x - w / 2 for x in xs], [coin_gt[c["file"]]["total"] for c in coins], w,
                      label="Gerçek", color=NAVY), "{:.2f}")
label_bars(ax, ax.bar([x + w / 2 for x in xs], [c["total"] for c in coins], w, label="Bulunan", color=RED),
           "{:.2f}")
ax.set_xticks(list(xs), [f"F{i + 1}" for i in xs])
ax.set_ylabel("Toplam (TL)")
ax.set_title("Bozuk para: gerçek ve bulunan toplam")
ax.legend(frameon=False)
fig.savefig(OUT / "coins_total.png")
plt.close(fig)

# 4) Göl: hesaplanan ve resmî alan
lake = after["lake"]
fig, ax = plt.subplots(figsize=(5.2, 4))
bars = ax.bar(["Resmî değer", "Programın hesabı"], [lake["referenceAreaKm2"], lake["areaKm2"]],
              color=[NAVY, TEAL], width=0.55)
for b in bars:
    ax.annotate(f"{b.get_height():,.0f} km²".replace(",", "."), (b.get_x() + b.get_width() / 2, b.get_height()),
                ha="center", va="bottom", fontsize=11, color=NAVY, xytext=(0, 2), textcoords="offset points")
err = abs(lake["areaKm2"] - lake["referenceAreaKm2"]) / lake["referenceAreaKm2"] * 100
ax.set_ylim(0, lake["referenceAreaKm2"] * 1.2)
ax.set_ylabel("Alan (km²)")
ax.set_title(f"Van Gölü alanı – hata %{err:.1f}".replace(".", ","))
fig.savefig(OUT / "lake_area.png")
plt.close(fig)

# 5) Otomatik eşik: 14 adayın puanı
cand = sorted(after["threshold"], key=lambda r: r["score"])
fig, ax = plt.subplots(figsize=(6.4, 4.6))
labels = [c["channel"] if c["channel"].startswith("RGB") else f"{c['channel']}{' (ters)' if c['inverted'] else ''}" for c in cand]
colors = [TEAL if c is cand[-1] else (AMBER if c["channel"].startswith("RGB") else (GREY if c["score"] > -50 else "#E3E7EA")) for c in cand]
ax.barh(labels, [max(c["score"], -3) for c in cand], color=colors)
ax.axvline(0, color="#5F6F7B", linewidth=0.8)
ax.set_xlabel("Puan (yüksek = daha iyi bölge)")
ax.set_title(f"Van Gölü için {len(cand)} eşik adayının puanı")
ax.tick_params(axis="y", labelsize=10)
fig.savefig(OUT / "threshold_scores.png")
plt.close(fig)

print("Grafikler:", sorted(p.name for p in OUT.glob("*.png")))
print("Göl hatası %", round(err, 2))
