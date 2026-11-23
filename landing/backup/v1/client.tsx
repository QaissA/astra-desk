"use client";
import { useEffect, useRef, type ImgHTMLAttributes } from "react";

// Fades .reveal elements in as they scroll into view.
export function RevealObserver() {
  useEffect(() => {
    const io = new IntersectionObserver(
      (entries) => entries.forEach((e) => {
        if (e.isIntersecting) { e.target.classList.add("in"); io.unobserve(e.target); }
      }),
      { threshold: 0.12 }
    );
    document.querySelectorAll(".reveal").forEach((el) => io.observe(el));
    return () => io.disconnect();
  }, []);
  return null;
}

// Hides itself when the file is missing, so the gradient placeholder shows until the image is added.
export function Img(props: ImgHTMLAttributes<HTMLImageElement>) {
  const ref = useRef<HTMLImageElement>(null);
  useEffect(() => {
    const img = ref.current;
    if (img?.complete && img.naturalWidth === 0) img.hidden = true; // error fired before hydration
  }, []);
  // eslint-disable-next-line @next/next/no-img-element, jsx-a11y/alt-text
  return <img ref={ref} {...props} onError={(e) => { e.currentTarget.hidden = true; }} />;
}
