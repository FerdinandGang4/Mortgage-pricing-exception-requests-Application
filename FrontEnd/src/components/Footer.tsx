import { Icon } from './Icon'

export function Footer({ year }: { year: number }) {
  return (
    <footer className="main-footer">
      <div className="footer-credit">
        <span className="footer-product">Blue Harvest <span className="footer-separator">/</span> Pricing Exceptions</span>
        <span className="developer-credit">Developed by <strong>Gang Ferdinand Dinga</strong></span>
        <address className="developer-contact">
          <a href="tel:+31626357541">Phone: +31626357541</a>
          <a href="mailto:dingaferdi2024@gmail.com">Email: dingaferdi2024@gmail.com</a>
        </address>
      </div>
      <div className="footer-links">
        <span className="footer-copyright">© {year} · Internal workspace</span>
        <a
          className="repository-link"
          href="https://github.com/FerdinandGang4/Mortgage-pricing-exception-requests-Application"
          target="_blank"
          rel="noopener noreferrer"
        >
          <Icon name="external" size={16} />
          View project on GitHub
        </a>
      </div>
    </footer>
  )
}
