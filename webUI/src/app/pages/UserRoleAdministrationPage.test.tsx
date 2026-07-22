import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { UserRoleAdministrationPage } from './UserRoleAdministrationPage';

const listUsersMock = vi.fn();
const listRolesMock = vi.fn();
const addRoleToUserMock = vi.fn();
const removeRoleFromUserMock = vi.fn();

vi.mock('../components/PageHeader', () => ({
  PageHeader: ({ title, subtitle }: { title: string; subtitle: string }) => (
    <header>
      <h1>{title}</h1>
      <p>{subtitle}</p>
    </header>
  ),
}));

vi.mock('../services/api', () => ({
  api: {
    listUsers: () => listUsersMock(),
    listRoles: () => listRolesMock(),
    addRoleToUser: (userId: string, roleId: number) => addRoleToUserMock(userId, roleId),
    removeRoleFromUser: (userId: string, roleId: number) => removeRoleFromUserMock(userId, roleId),
  },
}));

describe('UserRoleAdministrationPage', () => {
  beforeEach(() => {
    listUsersMock.mockReset();
    listRolesMock.mockReset();
    addRoleToUserMock.mockReset();
    removeRoleFromUserMock.mockReset();
    listUsersMock.mockResolvedValue([
      { id: 'user-1', username: 'sim-admin', email: 'sim@example.test', roles: ['Admin'] },
      { id: 'user-2', username: 'analyst', email: 'analyst@example.test', roles: [] },
    ]);
    listRolesMock.mockResolvedValue([
      { id: 1, name: 'Admin' },
      { id: 2, name: 'SimulationOperator' },
    ]);
    addRoleToUserMock.mockResolvedValue(null);
    removeRoleFromUserMock.mockResolvedValue(null);
  });

  it('loads users and roles and keeps role actions disabled until both selections exist', async () => {
    render(<UserRoleAdministrationPage />);

    expect(screen.getByRole('heading', { name: 'Users & Roles' })).toBeInTheDocument();
    expect(await screen.findByText('sim-admin')).toBeInTheDocument();
    expect(screen.getByText('analyst')).toBeInTheDocument();
    expect(screen.getByText('—')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Adicionar' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Remover' })).toBeDisabled();

    fireEvent.change(screen.getAllByRole('combobox')[0], { target: { value: 'user-2' } });
    expect(screen.getByRole('button', { name: 'Adicionar' })).toBeDisabled();
    fireEvent.change(screen.getAllByRole('combobox')[1], { target: { value: '2' } });
    expect(screen.getByRole('button', { name: 'Adicionar' })).toBeEnabled();
  });

  it('adds and removes selected roles then refreshes the table', async () => {
    render(<UserRoleAdministrationPage />);

    await screen.findByText('sim-admin');
    fireEvent.change(screen.getAllByRole('combobox')[0], { target: { value: 'user-2' } });
    fireEvent.change(screen.getAllByRole('combobox')[1], { target: { value: '2' } });

    fireEvent.click(screen.getByRole('button', { name: 'Adicionar' }));
    await waitFor(() => expect(addRoleToUserMock).toHaveBeenCalledWith('user-2', 2));
    expect(await screen.findByText('Role adicionada com sucesso.')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Remover' }));
    await waitFor(() => expect(removeRoleFromUserMock).toHaveBeenCalledWith('user-2', 2));
    expect(await screen.findByText('Role removida com sucesso.')).toBeInTheDocument();
    expect(listUsersMock).toHaveBeenCalledTimes(3);
    expect(listRolesMock).toHaveBeenCalledTimes(3);
  });

  it('reports load and update failures without hiding the form', async () => {
    listUsersMock.mockRejectedValueOnce(new Error('users unavailable'));

    const firstRender = render(<UserRoleAdministrationPage />);

    expect(await screen.findByText('users unavailable')).toBeInTheDocument();
    firstRender.unmount();

    listUsersMock.mockResolvedValue([{ id: 'user-1', username: 'sim-admin', email: 'sim@example.test', roles: [] }]);
    listRolesMock.mockResolvedValue([{ id: 2, name: 'SimulationOperator' }]);
    addRoleToUserMock.mockRejectedValueOnce(new Error('role denied'));

    render(<UserRoleAdministrationPage />);
    await screen.findByText('sim-admin');
    fireEvent.change(screen.getAllByRole('combobox')[0], { target: { value: 'user-1' } });
    fireEvent.change(screen.getAllByRole('combobox')[1], { target: { value: '2' } });
    fireEvent.click(screen.getByRole('button', { name: 'Adicionar' }));

    expect(await screen.findByText('role denied')).toBeInTheDocument();
  });
});
