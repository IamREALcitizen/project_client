# Night scene backgrounds

These 21 opaque 9:16 sprites are for the mobile night scenes: one scene background for each of the 20 character designs, plus `PIRATE_SHARED_Night.png` for a group of pirates. They contain only environments; place the separate chibi character sprites on top.

Use `NightSceneBackgrounds.asset` to select a sprite by the same `designId` used by `ChibiCharacterSkin`. For a Unity UI scene, add `NightSceneBackgroundView` to a full-stretch `Image`, assign the catalog in the Inspector, then call `ShowDesign(designId)` or `ShowPirateShared()` when the night scene opens. Keep the character renderers or UI images above the background in the canvas order.

The designs are optimized for 1080 × 1920 portrait UI. Set the Image to fill its RectTransform. If a device aspect ratio differs, crop at the edges rather than stretching the characters or scenery.
