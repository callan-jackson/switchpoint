import {
  BarChart3,
  ClipboardList,
  FileText,
  LayoutDashboard,
  LineChart,
  PiggyBank,
  ScrollText,
  Search,
  Settings,
  Users,
} from 'lucide-react'
import type { ComponentType } from 'react'

export interface NavItem {
  to: string
  label: string
  icon: ComponentType<{ className?: string }>
  /** Matches nested routes too (`/clients/:id`). */
  end?: boolean
}

export const navItems: NavItem[] = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard, end: true },
  { to: '/clients', label: 'Clients', icon: Users },
  { to: '/pension-switch', label: 'Pension Switch', icon: PiggyBank },
  { to: '/db-transfer', label: 'DB Transfer', icon: LineChart },
  { to: '/cashflow', label: 'Cashflow', icon: BarChart3 },
  { to: '/funds', label: 'Fund Research', icon: Search },
  { to: '/products', label: 'Products & Charges', icon: ClipboardList },
  { to: '/reports', label: 'Reports', icon: FileText },
  { to: '/audit', label: 'Audit', icon: ScrollText },
  { to: '/settings', label: 'Settings', icon: Settings },
]

/** Human label for a path segment, used by the breadcrumb trail. */
export const segmentLabels: Record<string, string> = {
  clients: 'Clients',
  'pension-switch': 'Pension Switch',
  'db-transfer': 'DB Transfer',
  cashflow: 'Cashflow',
  funds: 'Fund Research',
  products: 'Products & Charges',
  reports: 'Reports',
  audit: 'Audit',
  settings: 'Settings',
  new: 'New',
}
