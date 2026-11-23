# Landing page images

Save every image in **`landing\public\images\`** with the **exact file name** given. The page already links to these names, and until a
file exists its spot shows a cyan and violet gradient instead.

Rules for all images:
- **Original art only.** Don't use existing anime characters, studio styles by name, logos or watermarks.
- **No text in the image.** AI tools garble it, and the page adds the real text.
- Export **.webp** (quality ~80) at the size listed, except where it says .png.
- Brand colors to keep everything consistent: background `#05070d`, cyan `#22e4ff`, violet `#a64dff`, soft pink `#ff5fb7`.

**Shared style** (paste after each prompt):
> cinematic anime illustration, original character, dark night palette with neon cyan #22e4ff and violet #a64dff glow, soft pink
> accents, volumetric light, high detail, clean composition, futuristic, no text, no watermark, no logos

**Real screenshots beat generated ones** for `app-settings.webp` and `installer.webp`: they show the actual product. You can get
them from `DesktopStyler.exe --uitest <folder>`, which saves screenshots of every page.

---

## 1. `hero-desktop.webp`: 1920 × 1080 (16:9). The most important image.
A Windows 11 style desktop seen full screen. The wallpaper: an original anime girl with long silver hair and a dark techwear
jacket, standing on a rooftop at night above a neon city, cherry blossom petals drifting across the whole frame. Along the
bottom, a thin fully transparent glassy taskbar with small glowing app icons grouped left, center and right, and a slim neon
clock in the top right corner of the desktop. Feels alive and in motion, light streaks, depth of field.
*+ shared style*

## 2. `feature-wallpaper.webp`: 1600 × 800 (2:1)
An original anime scene shown as a desktop wallpaper: a girl with an umbrella on a rain-soaked neon street at night, heavy
rain streaks, puddle reflections in cyan and violet, a subtle digital glitch effect slicing across one edge of the image.
*+ shared style*

## 3. `feature-taskbar.webp`: 800 × 1100 (portrait)
Close-up, slightly angled view of the bottom of a desktop: a sleek, fully transparent floating taskbar made of frosted glass
with a thin cyan edge glow, round app icons, a small clock and system tray icons, sitting over a blurred colorful anime
wallpaper. Product-shot feel, shallow depth of field, glowing reflections.
*+ shared style*

## 4. `feature-workshop.webp`: 1200 × 750 (16:10)
A floating 3D grid of many wallpaper thumbnails tilted in perspective, each a different original anime landscape (sunset
city, starry sky, forest shrine, ocean, cyberpunk alley), one thumbnail in front highlighted with a cyan glowing border, the
grid fading into darkness.
*+ shared style*

## 5. `feature-clock.webp`: 1200 × 750 (16:10)
A large, elegant minimalist neon digital clock floating over a calm original anime night sky wallpaper with a crescent moon
and distant mountains. The digits are abstract glowing bars, not readable numbers. Lots of empty space, calm mood.
*+ shared style*

## 6. `feature-layout.webp`: 1200 × 750 (16:10)
A desktop seen from the front with four or five dark, glassy application windows snapping neatly into a tidy grid, motion
trails showing them sliding into place, thin cyan outlines, anime wallpaper softly visible behind. The windows contain only
abstract shapes, no readable UI text.
*+ shared style*

## 7. `feature-screensaver.webp`: 1200 × 750 (16:10)
A modern thin-bezel monitor in a dark room at night, its screen filled with a dreamy original anime scene of a girl looking
up at a galaxy and shooting stars. The screen glow lights up the desk and the air, cozy atmosphere.
*+ shared style*

## 8. `app-settings.webp`: 1200 × 750 (16:10). Use a real screenshot if you can.
If you generate it: a dark futuristic settings app window with a left sidebar of icons, glassy panels, toggle switches and
sliders glowing cyan and violet, a wallpaper preview card in the middle, floating above a blurred anime wallpaper.
*+ shared style*

## 9. `installer.webp`: 1200 × 900 (4:3). Use a real screenshot if you can.
If you generate it: a small, clean dark install dialog with a glowing app icon and two buttons, centered on a Windows 11 style
desktop with a soft anime wallpaper, gentle glow around the dialog, a reassuring, simple feel.
*+ shared style*

## 10. `donate-bg.webp`: 1920 × 900 (wide, sits behind text at 30% opacity)
Soft, abstract dreamy background: drifting cherry blossom petals and tiny glowing particles over a deep navy to violet
gradient, a faint warm heart-shaped glow in the center, mostly dark and empty so the text stays readable.
*+ shared style*

## 11. `og-image.png`: 1200 × 630. The preview shown when the link is shared on Discord, X or WhatsApp.
The hero scene from image 1 cropped wider and simpler: the silver-haired anime girl on the left third, neon city and sakura
petals, the right two thirds left dark and empty (you can add the title "Astra Desk" there yourself in an editor).
*+ shared style*

## Already done: `public\images\logo.png` and `app\icon.png` (favicon)
Exported from the app's real icon (`assets\app.ico`) so the site and the app match. Don't generate these.
