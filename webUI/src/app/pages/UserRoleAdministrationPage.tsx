import { useCallback, useEffect, useState } from 'react';
import { api } from '../services/api';
import type { AdminRoleResponse, AdminUserResponse } from '../types/operations';
import { PageHeader } from '../components/PageHeader';

export function UserRoleAdministrationPage() {
  const [users, setUsers] = useState<AdminUserResponse[]>([]);
  const [roles, setRoles] = useState<AdminRoleResponse[]>([]);
  const [selectedUser, setSelectedUser] = useState('');
  const [selectedRole, setSelectedRole] = useState('');
  const [message, setMessage] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    const [userResult, roleResult] = await Promise.all([api.listUsers(), api.listRoles()]);
    setUsers(userResult);
    setRoles(roleResult);
  }, []);

  useEffect(() => {
    void refresh().catch((value) =>
      setMessage(value instanceof Error ? value.message : 'Failed to load users and roles.'),
    );
  }, [refresh]);

  const changeRole = async (action: 'add' | 'remove') => {
    setMessage(null);
    try {
      if (action === 'add') {
        await api.addRoleToUser(selectedUser, Number(selectedRole));
      } else {
        await api.removeRoleFromUser(selectedUser, Number(selectedRole));
      }
      await refresh();
      setMessage(`Role ${action === 'add' ? 'adicionada' : 'removida'} com sucesso.`);
    } catch (value) {
      setMessage(value instanceof Error ? value.message : 'Role update failed.');
    }
  };

  return (
    <section className="ui-page">
      <PageHeader
        title="Users & Roles"
        subtitle="Administração de identidades separada de deployment e destroy; Admin não recebe esses poderes automaticamente."
        helpTopic="requestedResolved"
      />
      <div className="ui-card">
        <div className="ui-compare-row">
          <select value={selectedUser} onChange={(event) => setSelectedUser(event.target.value)}>
            <option value="">Utilizador</option>
            {users.map((user) => (
              <option key={user.id} value={user.id}>
                {user.username} · {user.roles.join(', ') || 'sem roles'}
              </option>
            ))}
          </select>
          <select value={selectedRole} onChange={(event) => setSelectedRole(event.target.value)}>
            <option value="">Role</option>
            {roles.map((role) => (
              <option key={role.id} value={role.id}>
                {role.name}
              </option>
            ))}
          </select>
          <button
            type="button"
            className="ui-button"
            disabled={!selectedUser || !selectedRole}
            onClick={() => void changeRole('add')}
          >
            Adicionar
          </button>
          <button
            type="button"
            className="ui-secondary"
            disabled={!selectedUser || !selectedRole}
            onClick={() => void changeRole('remove')}
          >
            Remover
          </button>
        </div>
        {message && <p className="ui-notice">{message}</p>}
      </div>
      <div className="ui-table-wrap">
        <table className="ui-table">
          <thead>
            <tr>
              <th>Utilizador</th>
              <th>Email</th>
              <th>Roles</th>
            </tr>
          </thead>
          <tbody>
            {users.map((user) => (
              <tr key={user.id}>
                <td>{user.username}</td>
                <td>{user.email}</td>
                <td>{user.roles.join(', ') || '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}


