// SEO guide pages, served at /<slug> by app/[slug]/page.tsx and listed in the sitemap.
export type Guide = {
  slug: string;
  nav: string; // short footer label
  title: string; // <title>, under ~60 chars
  description: string; // meta description, under ~160 chars
  h1: string;
  intro: string;
  sections: { h2: string; body: string[]; steps?: string[] }[];
  faq: [string, string][];
};

export const guides: Guide[] = [
  {
    slug: "animated-wallpaper-windows-11",
    nav: "Animated wallpaper on Windows 11",
    title: "How to Set an Animated Wallpaper on Windows 11 (Free)",
    description:
      "Windows 11 can't play video wallpapers on its own. Here's how to set a free animated or anime live wallpaper, with effects, in a few clicks.",
    h1: "How to set an animated wallpaper on Windows 11",
    intro:
      "Windows 11 only supports still pictures and slideshows as a desktop background. To get a moving wallpaper (a video, a GIF or an animated anime scene) you need a small app that plays it behind your desktop icons. This guide shows how to do it for free with Astra Desk.",
    sections: [
      {
        h2: "Why Windows 11 has no live wallpaper setting",
        body: [
          "In Settings > Personalization > Background you can pick a picture, a solid color, a slideshow or Windows Spotlight. None of them plays video. An app is needed that draws the animation on the desktop layer, under your icons and windows.",
          "A good live wallpaper app should also stop playing when you don't see it, so it doesn't cost you battery or frames in games.",
        ],
      },
      {
        h2: "Set an animated wallpaper with Astra Desk",
        body: ["Astra Desk is free, has no ads and needs no account."],
        steps: [
          "Download AstraDesk-Setup.exe from astra-desk.xyz and run it. It installs for your account only, no admin rights needed.",
          "Open the settings with Ctrl + Alt + S (or from the Start menu) and go to the Wallpaper page.",
          "Pick a video, GIF or image from your PC, or open the Workshop page to search thousands of anime wallpapers from Wallhaven, MoeWalls and MotionBgs, plus Pexels and Pixabay clips.",
          "Optionally add effects such as sakura petals, rain or glitch, and build a playlist that shuffles wallpapers.",
          "Done. Use the keyboard shortcuts to skip to the next wallpaper (W) or pause it (P).",
        ],
      },
      {
        h2: "Will a live wallpaper slow down my PC?",
        body: [
          "Astra Desk pauses the wallpaper by itself when a game or a full-screen video is running, when a window is maximized, when the PC is locked, and on battery if you turn that on. While paused it uses next to nothing.",
          "On multi-monitor setups the wallpaper covers every screen.",
        ],
      },
    ],
    faq: [
      ["Can I use a YouTube video as wallpaper?", "Download the video file first (only if you have the right to), then pick it in the Wallpaper page. Astra Desk plays local video files and GIFs."],
      ["Does it work on Windows 10?", "Astra Desk is made and tested for Windows 11."],
      ["Can the lock screen be animated too?", "The Windows lock screen only supports still pictures, so Astra Desk gives it a snapshot of your wallpaper. The animated version becomes your screensaver."],
    ],
  },
  {
    slug: "transparent-taskbar-windows-11",
    nav: "Transparent taskbar on Windows 11",
    title: "How to Make the Taskbar Fully Transparent on Windows 11",
    description:
      "Windows 11's transparency setting only tints the taskbar. Here's how to get a fully see-through, customizable taskbar for free.",
    h1: "How to make the taskbar transparent on Windows 11",
    intro:
      "Windows 11 has a transparency option, but it only makes the taskbar slightly see-through. If you want your wallpaper to show all the way to the bottom of the screen, you need a tool that replaces or restyles the taskbar.",
    sections: [
      {
        h2: "The built-in option (partial transparency)",
        body: ["This is the only setting Windows itself offers. The result is a frosted, tinted bar, not a clear one."],
        steps: ["Open Settings > Personalization > Colors.", "Turn on Transparency effects."],
      },
      {
        h2: "A fully transparent taskbar with Astra Desk",
        body: [
          "Astra Desk adds a custom bar that is fully transparent and that you design yourself: drag elements into left, center and right zones, pin your apps, and use one bar per screen.",
        ],
        steps: [
          "Download and install Astra Desk from astra-desk.xyz (free, installs for your account only).",
          "Open the settings with Ctrl + Alt + S and go to the Custom bar page.",
          "Turn the custom bar on, then arrange pinned apps, the clock and other elements into the zones you want.",
          "Pair it with an animated wallpaper from the Wallpaper page for the full effect.",
        ],
      },
      {
        h2: "Getting the normal taskbar back",
        body: [
          "Quit Astra Desk from the settings sidebar and the regular Windows taskbar comes back. Uninstalling from Settings > Apps also restores the taskbar, desktop icons, startup and screensaver settings.",
        ],
      },
    ],
    faq: [
      ["Is a transparent taskbar safe?", "Yes. Astra Desk doesn't patch Windows system files; it hides the Windows taskbar while its own bar runs and restores it when you quit."],
      ["Can I center the taskbar icons?", "Yes. Put your pinned apps in the center zone of the custom bar."],
      ["Does it support multiple monitors?", "Yes, you get one bar per screen."],
    ],
  },
  {
    slug: "wallpaper-engine-alternative",
    nav: "Free Wallpaper Engine alternative",
    title: "Free Wallpaper Engine Alternative for Windows 11",
    description:
      "Looking for a free alternative to Wallpaper Engine? Astra Desk gives Windows 11 animated anime wallpapers, effects, a transparent taskbar and more, at no cost.",
    h1: "A free Wallpaper Engine alternative for Windows 11",
    intro:
      "Wallpaper Engine is a popular paid app on Steam. If you want animated wallpapers without paying or without Steam, Astra Desk is a free option that also customizes the rest of your desktop.",
    sections: [
      {
        h2: "What you get with Astra Desk",
        body: [
          "Animated wallpapers from videos, GIFs or images, with Motion and Overlay effects like sakura petals, rain and glitch.",
          "A built-in Workshop that searches Wallhaven anime art and installs MoeWalls and MotionBgs clips in one click, plus Pexels and Pixabay.",
          "Extras most wallpaper apps don't have: a fully transparent custom taskbar, a desktop clock in five styles, an animated screensaver and one-click window tiling.",
          "Automatic pause during games, full-screen video, maximized windows and on battery.",
        ],
      },
      {
        h2: "Astra Desk vs Wallpaper Engine",
        body: [
          "Price: Astra Desk is free with no ads and no account. Wallpaper Engine is a paid Steam purchase.",
          "Wallpapers: Wallpaper Engine has its own huge Steam Workshop, including interactive scene wallpapers. Astra Desk focuses on video, GIF and image wallpapers pulled from free sources.",
          "Desktop customization: Astra Desk also replaces the taskbar, adds a clock, a screensaver and window layouts. Wallpaper Engine is wallpaper-only.",
          "Platform: Astra Desk is made for Windows 11 and installs without Steam.",
        ],
      },
      {
        h2: "How to switch",
        body: [],
        steps: [
          "Download Astra Desk from astra-desk.xyz and install it (no admin rights needed).",
          "Point the Wallpaper page at any video files you already have, or browse the Workshop page.",
          "Add effects and a playlist, then style the custom bar to match.",
        ],
      },
    ],
    faq: [
      ["Is Astra Desk really free?", "Yes. No ads, no account and no paid version. Donations are optional."],
      ["Can I use my Wallpaper Engine videos?", "Video wallpapers you have as regular files (MP4 and similar) can be picked in Astra Desk. Wallpaper Engine scene and web wallpapers are a different format and won't play."],
      ["Does Astra Desk need Steam?", "No. It's a standalone installer."],
    ],
  },
];
