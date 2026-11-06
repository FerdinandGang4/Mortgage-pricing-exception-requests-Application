type IconName = 'grid' | 'requests' | 'activity' | 'discount' | 'plus' | 'search' | 'arrow' | 'close' | 'check' | 'clock' | 'shield' | 'refresh' | 'copy' | 'file' | 'menu' | 'external' | 'alert' | 'sun' | 'moon'
const paths: Record<IconName, React.ReactNode> = {
  sun: <><circle cx="12" cy="12" r="4"/><path d="M12 2v2m0 16v2M2 12h2m16 0h2M5 5l1.5 1.5m11 11L19 19M5 19l1.5-1.5m11-11L19 5"/></>,
  moon: <path d="M20.5 13.4A8.5 8.5 0 0 1 10.6 3.5a8.5 8.5 0 1 0 9.9 9.9Z"/>,
  grid: <><rect x="3" y="3" width="7" height="7" rx="1.5"/><rect x="14" y="3" width="7" height="7" rx="1.5"/><rect x="3" y="14" width="7" height="7" rx="1.5"/><rect x="14" y="14" width="7" height="7" rx="1.5"/></>,
  requests: <><path d="M8 3h8l4 4v14H4V3z"/><path d="M15 3v5h5M8 12h8M8 16h5"/></>,
  activity: <><path d="M3 12h4l3-8 4 16 3-8h4"/></>,
  discount: <><path d="M20 4 4 20"/><circle cx="7" cy="7" r="3"/><circle cx="17" cy="17" r="3"/></>,
  plus: <path d="M12 5v14M5 12h14"/>,
  search: <><circle cx="10.5" cy="10.5" r="6.5"/><path d="m16 16 4.5 4.5"/></>,
  arrow: <path d="M5 12h14m-5-5 5 5-5 5"/>,
  close: <path d="m6 6 12 12M6 18 18 6"/>,
  check: <path d="m5 12 4 4L19 6"/>,
  clock: <><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></>,
  shield: <><path d="m12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6z"/><path d="m8 12 3 3 5-6"/></>,
  refresh: <><path d="M20 7v5h-5M4 17v-5h5"/><path d="M6 7a7 7 0 0 1 12-2l2 3M4 16l2 3a7 7 0 0 0 12-2"/></>,
  copy: <><rect x="8" y="8" width="12" height="13" rx="2"/><path d="M16 8V3H3v13h5"/></>,
  file: <><path d="M7 3h9l4 4v14H4V3h3M14 3v6h6M8 13h8M8 17h5"/></>,
  menu: <path d="M4 6h16M4 12h16M4 18h16"/>,
  external: <><path d="M14 3h7v7M21 3 11 13"/><path d="M10 3H3v18h18v-7"/></>,
  alert: <><path d="m12 3 10 18H2zM12 9v5M12 17h.01"/></>,
}
export function Icon({ name, size = 20, className = '' }: { name: IconName; size?: number; className?: string }) {
  return <svg className={className} width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{paths[name]}</svg>
}
