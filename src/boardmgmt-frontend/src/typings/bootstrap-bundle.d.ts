// Minimal types so TS accepts the dynamic/static import of the bundle.
declare module 'bootstrap/dist/js/bootstrap.bundle.min.js' {
  const mod: any;
  export default mod;
}

// Minimal, practical typings for Bootstrap's Modal used in your code.
// If your installed bootstrap already ships types, this will merge/augment.
declare module 'bootstrap' {
  // Narrowed to just what we use; extend as needed (Toast, Tooltip, etc.).
  export class Modal {
    constructor(element: Element, options?: any);
    show(): void;
    hide(): void;
    toggle(): void;
    handleUpdate(): void;
    dispose(): void;

    // Static helpers
    static getInstance(element: Element): Modal | null;
    static getOrCreateInstance(element: Element, options?: any): Modal;
  }
}

// If you want IntelliSense for a possible global bootstrap namespace:
declare global {
  interface Window {
    bootstrap?: any;
  }
}
