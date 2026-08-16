import { Navigate, Route, Routes } from 'react-router-dom'
import { AppHeader } from './components/AppHeader'
import { DevotionalPage } from './pages/DevotionalPage'

export default function App() {
  return (
    <div className="min-h-screen bg-neutral-50 dark:bg-neutral-950">
      <AppHeader />
      <Routes>
        <Route path="/" element={<Navigate to="/hoje" replace />} />
        <Route path="/hoje" element={<DevotionalPage />} />
        <Route path="/dia/:date" element={<DevotionalPage />} />
        <Route path="*" element={<Navigate to="/hoje" replace />} />
      </Routes>
    </div>
  )
}
