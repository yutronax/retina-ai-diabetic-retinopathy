namespace GoruntuIslemeOdevleri.UI;

/// <summary>Her sekmenin sağ panelinde gösterilen kullanım kılavuzu ve konu anlatımı.</summary>
internal static class Guides
{
    public static readonly GuideContent Nuts = new(
        "Ödev 1 · Ceviz ve fındık sayma",
        "Fotoğraftaki ceviz ve fındıkları bulur, sayar ve büyüklüklerine (piksel sayısı) göre ayırır.",
        [
            "\"Fotoğraf Aç\" ile beyaz kâğıt üzerinde çekilmiş ceviz-fındık fotoğrafını seç. Denemek için \"Örnek fotoğraflar\" menüsünden hazır bir fotoğraf seç.",
            "\"Analiz Et\"e bas.",
            "Sağdaki sonuçta her nesne farklı renge boyanır; üstünde sırası, türü (C = ceviz, F = fındık) ve piksel sayısı yazar.",
            "Alttaki kutuda fındık ve ceviz sayısı ile ayrım eşiğini oku.",
            "\"5 Örneği Doğrula\" beş örnek görüntüde gerçek ve bulunan sayıları tablo hâlinde gösterir.",
        ],
        [
            ("1. Maske", "Görüntü griye çevrilir ve Otsu eşiği uygulanır: zemin siyah, nesneler beyaz olur. Beyaz zemin renksiz, kuruyemiş kahverengi olduğu için renk doygunluğu (HSV-S) de eklenir."),
            ("2. Temizlik", "Açma (aşındır → genişlet) küçük noktaları siler, kapama (genişlet → aşındır) küçük boşlukları kapatır. Parlamanın açtığı delikler doldurulur; nesnelerin arasında kalan zemin cepleri ise boş bırakılır."),
            ("3. Değen nesneleri ayırma", "Mesafe dönüşümü her pikselin zemine uzaklığını verir; her nesnenin merkezi bir yerel tepe olur. Tepelerden başlayan watershed taşırması bölgeleri büyütür ve sınırı nesneler arasındaki dar boğaza çizer. Çok küçük kalan parçalar komşusuna katılır."),
            ("4. Bölge renklendirme", "Her ayrı beyaz bölgeye farklı renk verilir ve o renkteki piksel sayısı (alan) sayılır."),
            ("5. Sınıflandırma", "Alanlar küçükten büyüğe dizilir; art arda iki alan arasındaki en büyük oran (kaç kat büyük) ceviz ile fındığı ayıran eşik olur."),
            ("6. Renk doğrulaması", "Büyüklük yalnız görelidir: yalnız ceviz olan fotoğrafta da küçükler ve büyükler çıkar. Bu yüzden büyüklük ayrımı, küçük grup büyük gruptan belirgin şekilde daha kırmızıysa (Lab a farkı ≥ 3) kabul edilir; fındık kırmızımsı kahve, ceviz sarımsı bejdir. Renk farkı yoksa fotoğrafta tek tür vardır ve türüne ortalama renk tonuyla (H < 14 fındık) karar verilir."),
        ],
        [
            "Nesneler birbirine değmemeli; değen nesneler \"ayrılamadı\" olarak işaretlenir.",
            "Kamerayı zemine dik ve hep aynı yükseklikte tut, gölge yapma.",
            "Ödev en az 5 kendi çektiğin fotoğrafta denenmesini istiyor.",
        ]);

    public static readonly GuideContent Foreground = new(
        "Ödev 2 · Arka planı değiştirme",
        "Düz duvar önündeki kişiyi arka plandan ayırır ve istediğin yeni bir arka planın önüne yerleştirir.",
        [
            "\"Ön Plan Aç\" ile düz, tek renk duvar önünde çekilmiş kişi fotoğrafını seç.",
            "\"Arka Plan Aç\" ile yeni arka plan resmini seç (manzara, deniz vb.).",
            "Gerekirse \"GrabCut turu\" değerini artır (daha çok tur = daha dikkatli ayırma, daha yavaş).",
            "\"Arka Planı Değiştir\"e bas.",
            "Sağda kişinin yeni arka plan önündeki hâli görünür.",
            "\"Adımları Göster\" ara aşamaların altı görüntüsünü yan yana gösterir.",
        ],
        [
            ("1. Duvar rengi", "Üst kenar ve yan kenarların üst yarısından duvar örnekleri alınır. Duvar düz renkte değildir (ışık bir yandan gelir, köşeler kararır); bu yüzden duvar rengi konuma bağlı 2. derece bir yüzey olarak (en küçük kareler) modellenir."),
            ("2. Renk uzaklığı ve eşik", "Her pikselin Lab uzayında o konumdaki duvar rengine uzaklığı hesaplanır. Eşik duvar örneklerinin kendi dağılımından bulunur (%98'lik değer × 1,5 + 4). Eşiğin altında kalan ve kenara bağlı bölgeler duvardır; kişinin içindeki beyaz gömlek ya da gri sakal kenara bağlı olmadığı için silinmez."),
            ("3. GrabCut", "Kesin duvar (siyah), kesin kişi (beyaz) ve aradaki dar belirsiz şerit (gri) ile başlatılır. GrabCut renk modelleriyle yalnızca bu şeridi yeniden sınıflandırır."),
            ("4. Yumuşak kenar (alfa)", "Kenar pikseli kişi rengi F ile duvar rengi W'nin karışımıdır: I = αF + (1-α)W. α bu denklemden bulunur; saç telleri yarı saydam, ceket kenarı keskin olur."),
            ("5. Hale temizliği ve birleştirme", "Aynı denklemden F = (I - (1-α)W) / α çözülerek kenardaki beyaz duvar payı çıkarılır. Manzara oranı bozulmadan kırpılır, sonuç = αF + (1-α)·manzara."),
        ],
        [
            "Kıyafet ya da saç duvar rengine yakınsa sınır karışır; duvarla zıt renkte kıyafet seç.",
            "Kişinin başı fotoğrafın üst kenarına değmesin; duvar rengi oradan öğrenilir.",
            "Duvarda gölge olmasın; yumuşak, önden ışık kullan.",
        ]);

    public static readonly GuideContent Noise = new(
        "Ödev 3 · Tuz-biber gürültüsü ve morfoloji",
        "Rastgele siyah-beyaz noktaları (tuz-biber gürültüsü) farklı yöntemlerle temizler ve hangisinin daha iyi olduğunu ölçer.",
        [
            "\"Fotoğraf Aç\" ile temiz bir görüntü seç ya da \"Örnek fotoğraflar\" menüsünden birini aç.",
            "\"Gürültü %\" ile yoğunluğu seç, \"Gürültü Ekle\"ye bas: soldaki görüntü tuz-biberli olur.",
            "Şablon (9'lu kare / 5'li artı), İşlem (aşındır / genişlet / açma / kapama / medyan) ve Tur seç, \"Seçili İşlemi Uygula\"ya bas. PSNR ne kadar iyileştiğini gösterir.",
            "\"Sonucu Girdi Yap\" sonucu sola taşır; böylece önce aşındırıp sonra genişletmek gibi işlemleri zincirleyebilirsin. \"Sıfırla\" temiz görüntüye döner.",
            "R/G/B aralığını daraltırsan yalnız o renkteki pikseller komşu sayılır ve işlem yalnız onlara uygulanır (ör. R 150-255, G 0-90, B 0-90 = kırmızılar).",
            "\"Gürültü Karşılaştır\" iki şablonu ve medyanı aynı gürültüyle PSNR üzerinden kıyaslar.",
            "\"Kırık Kenar Deneyi\" 2-8 px açıklıklı çizgileri ve fotoğrafın kenarlarını iki şablonla kapatır, hangi açıklığın kapandığını tablo olarak verir.",
        ],
        [
            ("Aşındırma (erosion)", "Şablon bir pikselin üstüne konur; altındaki komşulardan biri bile siyahsa piksel siyah olur. Beyaz bölgeler incelir, beyaz noktalar (tuz) kaybolur."),
            ("Genişletme (dilation)", "Komşulardan biri beyazsa piksel beyaz olur. Beyaz bölgeler kalınlaşır, siyah noktalar (biber) kapanır."),
            ("Açma ve kapama", "Açma = aşındır + genişlet (küçük beyaz parçaları siler). Kapama = genişlet + aşındır (küçük boşlukları ve kopuklukları kapatır)."),
            ("9'lu kare ve 5'li artı", "Kare, merkez dahil 3×3 = 9 komşuya bakar. Artı yalnız merkez ve dört yönü (5 piksel) kullanır; ayrıntıyı daha az bozar."),
            ("Medyan filtresi", "Şablonun gösterdiği komşu değerleri sıralar ve ortadakini alır (aşındırma en küçüğü, genişletme en büyüğü alır). İşlem menüsünden 9'lu kare ya da 5'li artı şablonla seçilebilir. Tek tük aşırı değerler (gürültü) ortaya hiç çıkamaz, bu yüzden tuz-biber için çok etkilidir."),
            ("R/G/B aralığı", "Yalnız seçilen renk aralığındaki pikseller \"komşu\" (aynı nesne) sayılır. İşlem bu pikselleri gösteren maskeye uygulanır; genişletmede eklenen pikseller komşu nesne renginden, aşındırmada silinen pikseller çevredeki renklerden doldurulur. Örnek: R 150-255, G 0-90, B 0-90 sadece kırmızıları seçer."),
            ("PSNR", "Temiz görüntü ile sonuç arasındaki farkın desibel cinsinden ölçüsü. Yaklaşık 30 dB üstü gözle iyi görünür."),
        ],
        [
            "Adil karşılaştırma için tüm yöntemler aynı gürültüyle denenir (sabit rastgele tohum).",
        ]);

    public static readonly GuideContent Coins = new(
        "Ödev 4 · Bozuk para sayma",
        "Kâğıt üzerindeki 25 kuruş, 50 kuruş ve 1 TL'leri sayar ve toplam tutarı hesaplar; referans verilirse yazı-tura da ayırır.",
        [
            "\"Fotoğraf Aç\" ile beyaz kâğıt üzerinde, birbirine değmeyen paraların fotoğrafını seç. İçinde en az bir 1 TL olmalı.",
            "Yazı-tura için (isteğe bağlı) \"Yazı Referansı\" ve \"Tura Referansı\" ile tek paranın iki yüzünün fotoğrafını yükle.",
            "\"Paraları Analiz Et\"e bas.",
            "Sonuçta her para numaralanır, değeri ve yüzü (Y/T) yazılır; üstte toplam TL görünür.",
            "Denemek için \"Örnek fotoğraflar\" menüsünü kullan; yazı/tura referanslı sürüm için \"Sentetik örnek\"i seç.",
        ],
        [
            ("Paraları bulma", "Otsu eşiği ve morfoloji ile maske çıkarılır. Yuvarlaklık = 4π·alan / çevre²; 1'e yakın olan bölgeler para kabul edilir."),
            ("Değeri bulma", "Her paranın çapı ölçülür. En büyük para 1 TL (26,15 mm) kabul edilir ve piksel/mm ölçeği ondan bulunur. Diğer çaplar 50 kr (23,85 mm) ve 25 kr (20,50 mm) ile karşılaştırılır."),
            ("Yazı-tura", "Para görüntüsü 15°'lik adımlarla döndürülen yazı ve tura referanslarıyla şablon eşlemeyle karşılaştırılır; daha benzer olan seçilir."),
        ],
        [
            "Desenli veya renkli zeminde paralar zeminden ayrılamaz.",
            "Üst üste ya da birbirine değen paralar tek leke olur ve elenir.",
        ]);

    public static readonly GuideContent Lake = new(
        "Ödev 5 · Uydu görüntüsünde alan ve çevre",
        "Uydu fotoğrafındaki gölün gerçek alanını (km²) ve çevresini (km) bulur. Program piksel sayar; bir pikselin kaç metre olduğunu ona sen ölçek çizgisiyle söylersin.",
        [
            "\"Uydu Görüntüsü Aç\" ile fotoğrafı yükle. Ölçmek istediğin göl resmin ortasına yakın olsun.",
            "\"Ölçeği 2 Noktayla Seç\"e bas ve aradaki gerçek mesafesini bildiğin iki noktaya tıkla (iki kasaba, iki dağ zirvesi ya da resimdeki ölçek çubuğunun uçları). \"Piksel uzunluğu\" kendiliğinden dolar.",
            "\"Gerçek uzunluk (m)\" kutusuna bu iki nokta arasındaki gerçek mesafeyi METRE olarak yaz (55 km → 55000). Mesafeyi Google Haritalar'daki \"Mesafeyi ölç\" ile bulabilirsin.",
            "\"Alan ve Çevreyi Hesapla\"ya bas.",
            "Sonuçta gölün kenarı kırmızı çizilir; alttaki kutuda ölçek, alan ve çevre yazar.",
        ],
        [
            ("Ölçek", "metre/piksel = gerçek uzunluk ÷ piksel uzunluğu. Örn. 55000 m ÷ 633 piksel ≈ 86,8 m/piksel."),
            ("Eşikleme ve bölgeler", "Su koyu olduğu için otomatik eşik suyu beyaz, karayı siyah yapar. Her ayrı beyaz bölge farklı renge boyanır."),
            ("Hangisi göl?", "Her bölgenin ağırlık merkezi (piksellerinin x ve y ortalaması) bulunur. Resmin ortasına (+ işareti) en yakın olan göl kabul edilir."),
            ("Alan", "Göl bölgesindeki piksel sayısı × (metre/piksel)². Sonuç m² olur; 1.000.000'a bölünce km²."),
            ("Çevre", "Göl bir piksel genişletilir ve orijinali çıkarılır; geriye tek piksellik kenar kalır. Kenar pikseli sayısı × metre/piksel = çevre."),
        ],
        [
            "Ölçek %10 yanlışsa alan yaklaşık %20 yanlış çıkar (alan ölçeğin karesiyle büyür). Ölçek noktalarını dikkatli seç.",
            "Van Gölü'nün resmî alanı yaklaşık 3.755 km²; kendi sonucunu bununla karşılaştırabilirsin.",
        ]);

    public static readonly GuideContent Threshold = new(
        "Araştırma · Otomatik eşik seçimi",
        "Bir görüntüyü siyah-beyaza çevirirken en uygun eşiği ve renk kanalını otomatik bulmaya çalışır.",
        [
            "\"Görüntü Aç\" ile bir fotoğraf seç ya da \"Göl Örneği\" / \"Ceviz-Fındık Örneği\"ne bas.",
            "\"Otomatik Eşiği Araştır\"a bas.",
            "Sağda en yüksek puanlı adayların maskeleri; altta her adayın kanalı, eşik değeri (T) ve puanı (S) listelenir.",
        ],
        [
            ("Eşikleme", "Eşik değerinden parlak pikseller beyaz, koyular siyah yapılır. Doğru eşik nesneyi zeminden net ayırır."),
            ("Otsu yöntemi", "Histogramı iki gruba ayıran ve gruplar arası varyansı en büyük yapan eşiği kendisi seçer."),
            ("16 aday", "Gri, B, G, R, H, S, V kanallarının her birinde normal ve ters maske (14 aday) ile R, G, B kanallarına ayrı ayrı eşik uygulayıp birleştiren 2 aday denenir."),
            ("Puan", "puan = 2,4 × merkeze yakınlık + 1,1 × uygun alan + 0,45 × kompaktlık − 1,8 × kenara temas. Hedefin görüntü ortasında olduğu varsayılır."),
        ],
        [
            "Puan evrensel değildir; hedef nesne kenarda ise yanlış aday kazanabilir.",
        ]);
}
