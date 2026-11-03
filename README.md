# ParamBridge — Revit ⇄ Excel parametre senkronizasyon eklentisi

Bu paket, DiRoots SheetLink'in **çalışma şeklini** (işlevsel davranışını) örnek alan, ama kodu, arayüz tasarımı ve ikonları tamamen özgün olan bir Revit eklentisi geliştirmek için hazırlandı. İçinde:

| Klasör / dosya | Ne işe yarar |
|---|---|
| `docs/01_FEATURE_SPEC.md` | Eklentinin yapacağı her şeyin işlevsel şartnamesi (sekmeler, akışlar, kurallar, hata mesajları) |
| `docs/02_ARCHITECTURE.md` | Katmanlar, sınıflar, Revit API kullanım kuralları, threading (ExternalEvent) |
| `docs/03_UI_SPEC.md` | Özgün arayüz tasarımı: yerleşim, renk token'ları, kontroller |
| `docs/04_EXCEL_FORMAT.md` | Excel dosya sözleşmesi: gizli satır/sütun, renk kodları, Instructions sayfası |
| `docs/05_AGENT_INSTRUCTIONS.md` | **DeepSeek'e (veya başka bir kodlama ajanına) verilecek ana talimat**: kilometre taşları, kabul testleri, kurallar |
| `docs/06_REA_FINDINGS.md` | Rea ile DiRoots One 2.4.1.0 üzerinde yapılan statik analizin özeti (yalnızca özellik yüzeyi) |
| `src/` | Başlangıç C# çözümü (.NET 8, WPF, MVVM, Revit 2025/2026) |

## Hızlı başlangıç (Revit olan Windows makinede)

1. Visual Studio 2022 (17.8+) veya `dotnet` SDK 8 kurulu olsun.
2. `src/ParamBridge.sln` dosyasını açın, **Release | x64** derleyin.
   - Revit API referansları NuGet'ten (`Nice3point.Revit.Api.*`) gelir, Revit kurulu klasöre referans gerekmez.
   - Varsayılan hedef Revit 2025. Revit 2026 için: `dotnet build -c Release -p:RevitVersion=2026`
3. Derleme sonunda `ParamBridge.addin` ve DLL'ler `%AppData%\Autodesk\Revit\Addins\2025\` altına otomatik kopyalanır (csproj içindeki `DeployToRevit` hedefi). İstemezseniz `-p:DeployToRevit=false`.
4. Revit'i açın → **Add-Ins** sekmesi → **ParamBridge** paneli → **Excel Sync** butonu.

> Not: Bu iskelet Linux/Mac üzerinde yazıldı, Revit karşısında henüz derlenip test edilmedi. İlk derlemede küçük API/derleme hataları çıkabilir; `05_AGENT_INSTRUCTIONS.md` ajanın bunları nasıl ele alacağını tarif ediyor.

## Claude Code ile çalışma

Revit kurulu Windows makinede paket klasöründe `claude` çalıştırın. Kökteki `CLAUDE.md` derleme komutlarını ve kuralları içerir. İlk mesaj:

> docs/05_AGENT_INSTRUCTIONS.md'yi oku, M0'dan başla. Her kilometre taşından sonra derle, bana Revit'te yapacağım test adımlarını ver ve dur.

## DeepSeek ile çalışma

Diğer bilgisayarda ajana ilk mesaj olarak şunu verin:

> `docs/05_AGENT_INSTRUCTIONS.md` dosyasını oku ve oradaki kilometre taşlarını sırayla uygula. Her kilometre taşından sonra derle, kabul testlerini listele ve dur.

Ajan sırasıyla `01` → `04` dokümanlarını okuyup `src/` altındaki iskeleti tamamlayacak.

## Hukuki çerçeve (önemli)

- DiRoots'un kodu, XAML'ı, ikonları, metinleri veya Excel şablonları **kopyalanmadı**; DiRoots One DLL'i obfuscate ve lisanslı olduğundan iç mantığı da çözülmedi.
- `06_REA_FINDINGS.md` yalnızca dışarıdan görülebilen özellik yüzeyini (ekran/sınıf adları, kullanılan kütüphaneler, kullanıcıya gösterilen özellik kavramları) özetler; metinler kendi kelimelerimizle yeniden yazıldı.
- Ürün adı, ikonlar ve renkler özgündür. "SheetLink" adını ürününüzde veya pazarlamada kullanmayın.
