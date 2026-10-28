using System;
using System.IO;
using System.Linq;

namespace Negotchi
{
    internal sealed partial class NegotchiApp
    {
        private bool creatingPet;
        private bool registeredPetForTurn;
        private string petDraftDirectory;
        private string PetCreationPrompt(string text)
        {
            registeredPetForTurn = false;
            petDraftDirectory = Path.Combine(dataDirectory, "pet-drafts", Guid.NewGuid().ToString("N"));
            return text + "\n\nCrie o pacote final em " + petDraftDirectory + ". Use onze estados, incluindo thinking e responding, alem dos nove do Guts. Execute o validador e inspecione as previas antes de concluir. Se negotchi_import_pet estiver disponivel, registre o pacote com ela. Caso contrario, o aplicativo verifica essa pasta ao concluir o pedido. Se a geracao de imagens nao estiver disponivel nesta sessao, informe a limitacao; nao simule animacoes usando uma imagem estatica.";
        }
        private void FinishPetCreation(bool success)
        {
            if (!creatingPet) return;
            try
            {
                if (success && !registeredPetForTurn && !string.IsNullOrEmpty(petDraftDirectory) && File.Exists(Path.Combine(petDraftDirectory, "pet.json")))
                {
                    // Older resumed threads may not have the new dynamic tool. The known draft
                    // directory is checked locally; paths are never extracted from model prose.
                    PetPackages.LoadCreated(petDraftDirectory);
                    RegisterPet(Path.Combine(petDraftDirectory, "pet.json"), Path.Combine(petDraftDirectory, "spritesheet.png"));
                }
            }
            catch (Exception error) { AddSystem("O pacote criado nao foi importado: " + error.Message); Status(error.Message, "failed"); }
            finally { creatingPet = false; petDraftDirectory = null; }
        }
        private void PublishPetActivity() { chat.SetStatus(activity.Text); pet.Mood(activity.Text, activity.Animation, busy); }
        private void RefreshPetCatalog()
        {
            catalog = new PetCatalog(Path.Combine(dataDirectory, "pets"));
            chat.RefreshPets(catalog);
            if (!busy && !commandBusy && !catalog.Pets.Any(p => p.Id == activeAssets.Id)) SelectPet(catalog.Find(null).Id);
            if (catalog.Errors.Count > 0) chat.SetStatus("Pets locais ignorados: " + string.Join("; ", catalog.Errors));
        }
        private string RegisterPet(string manifest, string sheet)
        {
            string id = PetPackages.Install(manifest, sheet, Path.Combine(dataDirectory, "pets"), catalog.Pets.Select(p => p.Id));
            RefreshPetCatalog();
            AddSystem("Pet " + id + " adicionado a aba Pets."); Save(); return id;
        }
        internal static string FindCreatePetSkill()
        {
            // The build ships a self-contained copy; source runs can discover the repo skill.
            string bundled = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skills", "create-pet", "SKILL.md");
            if (File.Exists(bundled)) return bundled;
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                string path = Path.Combine(directory.FullName, ".agents", "skills", "create-pet", "SKILL.md");
                if (File.Exists(path)) return path;
                directory = directory.Parent;
            }
            throw new FileNotFoundException("A skill create-pet nao acompanha esta compilacao. Execute desktop/build.ps1 novamente.");
        }
        private void CreatePet(string description)
        {
            try
            {
                var skill = new SkillChoice { Name = "create-pet", DisplayName = "Criar pet animado", Description = "Onze animacoes para o Negotchi", Path = FindCreatePetSkill(), Enabled = true };
                ClearPendingSkill(); pendingSkill = skill; chat.SetSkill(skill.DisplayName); OpenChat(); chat.ShowConversation();
                if (string.IsNullOrWhiteSpace(description))
                {
                    chat.PrefixInput("$create-pet ");
                    Status("Descreva o personagem e o estilo; voce tambem pode anexar uma imagem de referencia pelo caminho.", "waving");
                    return;
                }
                SendMessage("$create-pet " + description);
            }
            catch (Exception error) { AddSystem(error.Message); Status(error.Message, "failed"); }
        }
        private void ImportPet()
        {
            OpenChat();
            var manifest = new Microsoft.Win32.OpenFileDialog { Title = "Escolha o pet.json", Filter = "Manifesto de pet|pet.json", CheckFileExists = true };
            if (manifest.ShowDialog(chat) != true) return;
            string sheet = Path.Combine(Path.GetDirectoryName(manifest.FileName), "spritesheet.png");
            if (!File.Exists(sheet))
            {
                var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Escolha o spritesheet do pet", Filter = "Spritesheet PNG|*.png", CheckFileExists = true };
                if (dialog.ShowDialog(chat) != true) return;
                sheet = dialog.FileName;
            }
            try { string id = RegisterPet(manifest.FileName, sheet); SelectPet(id); chat.ShowPets(); }
            catch (Exception error) { AddSystem("Nao foi possivel importar o pet: " + error.Message); Status(error.Message, "failed"); Save(); }
        }
    }
}
