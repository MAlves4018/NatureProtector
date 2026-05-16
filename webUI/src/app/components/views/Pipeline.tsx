import { getColors } from "../../utils/utils";


export function Pipeline({ isDark }: { isDark: boolean }) {
    const c = getColors(isDark);
    return (
        <div style={{ color: c.textPrimary, padding: '20px' }}>
            <h1>Pipeline View</h1>
            <p>This is where the pipeline details will be displayed.</p>
        </div>
    );
}