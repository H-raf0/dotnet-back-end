using System.ComponentModel.DataAnnotations;

public record UserLogin(
    
    [Required(ErrorMessage = "L'email est obligatoire.")]
    [EmailAddress(ErrorMessage = "L'email doit être une adresse e-mail valide.")]
    string Email,
    
    [Required(ErrorMessage = "Le mot de passe est obligatoire.")]
    [StringLength(20, MinimumLength = 4, ErrorMessage = "Le mot de passe doit contenir entre 4 et 20 caractères.")]
    [RegularExpression(@"^[a-zA-Z0-9&^!@#]+$", ErrorMessage = "Le mot de passe doit contenir uniquement des lettres, des chiffres et les caractères spéciaux &^!@#.")]
    string Password
);