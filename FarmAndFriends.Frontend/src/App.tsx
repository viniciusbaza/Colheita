import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import Login from './pages/Login'
import Game from './pages/Game'
import Register from './pages/Register'
import { useAuth } from './auth/useAuth'

function PrivateRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated } = useAuth()
  return isAuthenticated ? <>{children}</> : <Navigate to="/login" />
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<Login />} />
        <Route path="/register" element={<Register />} />
        <Route path="/game" element={<PrivateRoute><Game /></PrivateRoute>} />
        <Route path="*" element={<Navigate to="/game" />} />
      </Routes>
    </BrowserRouter>
  )
}