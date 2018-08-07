# NTeleportation
This is a fork of the NTeleportation at https://oxidemod.org/plugins/n-teleportation.1832/.  It is hoped to be temporary.

Here, we have added the ability to bypass cooldown timers using the Economics plugin (https://umod.org/plugins/economics) to deduct a preset amount from a user's balance.

The relevant config changes are:

1. Under Settings, "BypassCMD": "pay" (The extra word to type to bypass - global)
2. Under each section for Town or Home, "Bypass": 2 (It will charge 2 coins if they elect to pay)

So, /town pay would bypass a cooldown for /town, and /home 1 pay would bypass a cooldown for /home 1.
I'm sure there is room for improvement.

