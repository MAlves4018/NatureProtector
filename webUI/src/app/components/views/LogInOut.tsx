import { useState } from "react";
import { LogIn, LogOut } from "lucide-react";
import { api } from "../../services/api";
import { getColors } from "../../utils/utils";
import { useToken } from "../../context/TokenContext";
import { useNavigate } from "react-router";
import { backdropStyle, cardStyle, iconRing, inputStyle, labelStyle, primaryButton, wrapperStyle } from "../styles/styleUtils";
import { LogInOutProps } from "../../types";

export function LogInOut({ isDark, message, onAuthChange, mode = "page" }: LogInOutProps) {
	const c = getColors(isDark);
	const [usernameOrEmail, setUsernameOrEmail] = useState("");
	const [password, setPassword] = useState("");
	const [loading, setLoading] = useState(false);
	const [error, setError] = useState<string | null>(null);
	const { token, login, logout } = useToken();
	const nav = useNavigate();

	const handleLogin = async () => {
		setLoading(true);
		setError(null);
		try {
			const resp = await login(usernameOrEmail, password);
			setPassword("");
			onAuthChange?.(true);
			nav(-1);
		} catch (err) {
			setError(err instanceof Error ? err.message : "Failed to sign in.");
			onAuthChange?.(false);
		} finally {
			setLoading(false);
		}
	};

	const handleLogout = async () => {
		setLoading(true);
		setError(null);
		try {
			await api.logout();
			logout();
			onAuthChange?.(false);
			nav(-1);
		} catch (err) {
			setError(err instanceof Error ? err.message : "Failed to sign out.");
		} finally {
			setLoading(false);
		}
	};

	const signedIn = Boolean(token);
	const isPanel = mode === "panel";

	return (
		<div style={wrapperStyle(c, isPanel)}>
			<div style={backdropStyle(isPanel ? c.panelBg : c.pageBg)} />
			<section style={cardStyle(c, isPanel)}>
				<div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
					<div style={{ ...iconRing(c), color: signedIn ? "#16a34a" : "#d97706" }}>
						<img src="./images/NPIconNoBg.png" size={18} />
					</div>
					<div>
						<div style={{ color: c.textPrimary, fontSize: "20px", fontWeight: 800 }}>
							{signedIn ? "Access granted" : "Sign In Required"}
						</div>
					</div>
				</div>

				{signedIn ? (
					<div style={{ marginTop: "18px", display: "grid", gap: "12px" }}>
						<button
							onClick={handleLogout}
							disabled={loading}
							style={primaryButton(c, "#ef4444")}
						>
							<LogOut size={16} /> Sign out
						</button>
					</div>
				) : (
					<div style={{ marginTop: "18px", display: "grid", gap: "12px" }}>
						<label style={labelStyle(c)} htmlFor="usernameOrEmail">Username or email</label>
						<input
							id="usernameOrEmail"
							style={inputStyle(c)}
							value={usernameOrEmail}
							onChange={event => setUsernameOrEmail(event.target.value)}
							placeholder="user@domain.pt"
							autoComplete="username"
						/>
						<label style={labelStyle(c)} htmlFor="password">Password</label>
						<input
							id="password"
							style={inputStyle(c)}
							type="password"
							value={password}
							onChange={event => setPassword(event.target.value)}
							placeholder="password"
							autoComplete="current-password"
						/>
						<button
							onClick={handleLogin}
							disabled={loading || !usernameOrEmail || !password}
							style={primaryButton(c, "#16a34a")}
						>
							<LogIn size={16} /> {loading ? "Signing in..." : "Sign in"}
						</button>
					</div>
				)}

				{error && (
					<div style={{ marginTop: "12px", color: c.redText, fontSize: "13px" }}>
						{error}
					</div>
				)}
			</section>
		</div>
	);
}
