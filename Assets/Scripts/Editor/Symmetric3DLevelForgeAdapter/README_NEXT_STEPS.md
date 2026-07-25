# Symmetric3D — LevelForge Adapter Durumu

LevelForge Kurulum Sihirbazı ile üretilen iskelet, bu proje için **elle dolduruldu** (sihirbazın
kendisini tekrar çalıştırırsan sadece TODO'lu iskelet üretir — aşağıdaki "gerçek" mantık kaybolur,
tekrar elle taşımak gerekir).

- ✅ `Symmetric3DGenerationParams.cs` — hazır.
- ✅ `Symmetric3DParameterSpace.cs` — hazır (jenerik, min/max'a göre otomatik üretildi).
- ✅ `Symmetric3DDifficultyTiers.cs` — hazır (Kolay .15 / Orta .40 / Zor .65 / Uzman .85).
- ✅ `Symmetric3DCandidate.cs` — dolduruldu: mevcut bir `LevelData` asset'ini sarıyor.
- ✅ `Symmetric3DDifficultyEvaluator.cs` — dolduruldu, **AMA HEURİSTİK**: parça sayısı, renk/tarif
  çeşitliliği, aktif mekanik bayrak sayısı (Rotation/ColorMix/Shadow/Linked/QuarterFill) ve
  ortalama doldurma ihtiyacından ağırlıklı bir zorluk tahmini üretir. BlockMerge3D'nin
  `LevelSolver`'ının aksine board'un GERÇEKTEN temizlenebilir olup olmadığını aramaz — bunu
  yapan bir move-search çözücü bu proje için henüz yazılmadı (bilinçli kapsam kararı).

## Doğrulama

`Symmetric3D/LevelForge/Mevcut Levelleri Heuristik ile Puanla` menüsü (`Symmetric3DHeuristicSelfTest.cs`),
`Assets/Levels/` altındaki var olan 10 level asset'ini bu evaluator ile puanlayıp Console'a yazar —
üretilen kodun gerçek proje verisi üzerinde çalıştığının kanıtı.

## Sonraki adım (opsiyonel): gerçek üretim döngüsü

Şu an sadece var olan (elle tasarlanmış) levelleri PUANLIYORUZ; yeni level ÜRETMİYORUZ. Bunun için
kendi prosedürel üreticini yazıp (`Func<Symmetric3DGenerationParams, Symmetric3DCandidate>`)
şunu çağırabilirsin:

```csharp
var engine = new LevelForge.DifficultySearchEngine();
var tier = Symmetric3DDifficultyTiers.GetTier("Orta");
var result = engine.Run(
    initialParams: new Symmetric3DGenerationParams { pieceCount = 12, distinctColorCount = 3, maxSlices = 4 },
    tier: tier,
    generate: p => /* senin prosedürel üreticin — LevelData oluşturup Symmetric3DCandidate'e sar */ null,
    evaluator: new Symmetric3DDifficultyEvaluator(),
    paramSpace: new Symmetric3DParameterSpace(),
    budget: new LevelForge.SearchBudget());
```

Ayrıntılı rehber: `Packages/com.fogboundgames.levelforge/ADAPTER_GUIDE.md`.
