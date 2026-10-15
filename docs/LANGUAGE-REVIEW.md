# Language review

WP06-R4: Hlumela reviews a sample of 20 cards in Afrikaans (`af`) and 20 in isiZulu (`zu`) from the seed-7 match
and records each correction here. Corrections become worked examples in version 2 of the translator prompt
(`src/Pitchside.Agents/prompts/translator-v2.md`).

The sample must come from the Foundry provider. The scripted provider used offline and in tests returns the English
sentences unchanged, so there is nothing to review until the cards are generated against the deployed models:

```bash
dotnet run --project src/Pitchside.Cli -- narrate --seed 7 --provider foundry --personas all --languages af,zu > out/seed-7-af-zu.txt
```

## Corrections

| Card id | Language | Shown | Corrected | Note |
| --- | --- | --- | --- | --- |
