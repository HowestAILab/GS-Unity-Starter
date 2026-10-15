# Ingebed in het startproject

Kopie van https://github.com/ninjamode/Unity-VR-Gaussian-Splatting op commit
9675b7a60ab359f233f0e95671ae699bdf8b1e69, met één aanpassing voor Unity 6000.6:

- `Editor/4DGS/GaussianSplat4DEditor.cs`: `GetInstanceID()` → `GetHashCode()` (2×).
  In Unity 6000.6 is `Object.GetInstanceID()` een compileerfout (obsolete → `GetEntityId`).
  `Object.GetHashCode()` geeft dezelfde instance-id terug, dus het gedrag blijft gelijk.

Bij een nieuwere versie van de fork: deze map vervangen en de aanpassing opnieuw doen
(of controleren of ze upstream al gebeurd is).
