# OatmealDome.NinLib.MessageStudio

This library allows you to read MSBT files generated by Nintendo's MessageStudio suite. Writing is not possible at this time.

## Usage

```csharp
byte[] msbtData = File.ReadAllBytes("Messages.msbt");
Msbt msbt = new Msbt(msbtData);

// Get the text associated with a label
string message = msbt.Get("Label");

// Check if a label exists in this MSBT
bool doesLabelExist = msbt.ContainsKey("MysteryLabel");
```
